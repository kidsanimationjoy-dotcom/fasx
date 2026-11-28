(() => {
  "use strict";

  const status = document.getElementById("status");
  const video = document.getElementById("remoteVideo");
  const controls = document.getElementById("remoteControls");
  const metrics = document.getElementById("connectionMetrics");
  const textInput = document.getElementById("remoteText");
  const sendTextButton = document.getElementById("sendTextButton");
  let peer = null;
  let inputChannel = null;
  let controlAllowed = false;
  let pointerDown = null;
  let lastMoveAt = 0;
  let statsTimer = null;
  let previousVideoStats = null;

  function report(message) {
    status.textContent = message;
    if (window.Fasx && typeof window.Fasx.reportStatus === "function") {
      window.Fasx.reportStatus(message);
    }
  }

  function sendInput(command) {
    if (!controlAllowed || !inputChannel || inputChannel.readyState !== "open") return false;
    try {
      inputChannel.send(JSON.stringify(command));
      return true;
    } catch (_) {
      return false;
    }
  }

  function toScreenPoint(clientX, clientY) {
    const rect = video.getBoundingClientRect();
    const width = video.videoWidth;
    const height = video.videoHeight;
    if (!width || !height || rect.width <= 0 || rect.height <= 0) return null;

    // The video uses object-fit: contain; remove any letterboxing before normalizing.
    const scale = Math.min(rect.width / width, rect.height / height);
    const contentWidth = width * scale;
    const contentHeight = height * scale;
    const left = rect.left + (rect.width - contentWidth) / 2;
    const top = rect.top + (rect.height - contentHeight) / 2;
    const x = (clientX - left) / contentWidth;
    const y = (clientY - top) / contentHeight;
    if (x < 0 || x > 1 || y < 0 || y > 1) return null;
    return { x, y };
  }

  function waitForIceGatheringComplete(connection, timeoutMs = 15000) {
    if (connection.iceGatheringState === "complete") return Promise.resolve();

    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        cleanup();
        reject(new Error("ICE candidate gathering timed out."));
      }, timeoutMs);

      const onStateChange = () => {
        if (connection.iceGatheringState === "complete") {
          cleanup();
          resolve();
        }
      };

      function cleanup() {
        clearTimeout(timeout);
        connection.removeEventListener("icegatheringstatechange", onStateChange);
      }

      connection.addEventListener("icegatheringstatechange", onStateChange);
    });
  }

  async function startFasxRemoteView(offerSdp, allowControlInput = false) {
    stopFasxRemoteView();
    controlAllowed = allowControlInput === true;
    report("Connecting to the approved Windows screen…");

    try {
      const connection = new RTCPeerConnection({ iceServers: [] });
      peer = connection;

      connection.addTransceiver("video", { direction: "recvonly" });

      connection.ondatachannel = (event) => {
        const channel = event.channel;
        if (!controlAllowed || channel.label !== "fasx-control") {
          channel.close();
          return;
        }

        inputChannel = channel;
        channel.onopen = () => {
          controls.hidden = false;
          video.style.touchAction = "none";
          report("Connected. Windows approved view and limited input.");
        };
        channel.onclose = () => {
          controls.hidden = true;
          video.style.touchAction = "pan-y";
          inputChannel = null;
          report("Remote input channel closed. Screen viewing may continue.");
        };
        channel.onerror = () => {
          report("Remote input channel encountered a connection error.");
        };
      };

      connection.ontrack = async (event) => {
        const stream = event.streams && event.streams[0];
        if (stream) {
          video.srcObject = stream;
          try { await video.play(); } catch (_) { /* inline playback may need a user tap */ }
          report(controlAllowed
            ? "Receiving the Windows screen. Waiting for the approved input channel…"
            : "Receiving the Windows screen. View-only mode is active.");
          startConnectionStats(connection);
        }
      };

      connection.onconnectionstatechange = () => {
        const state = connection.connectionState;
        if (state === "connected") {
          if (!controlAllowed) report("Connected. View-only screen stream is active.");
        } else if (state === "disconnected" || state === "failed" || state === "closed") {
          video.srcObject = null;
          controls.hidden = true;
          report(
            state === "disconnected"
              ? "Screen connection interrupted. The Windows sender may be stopping."
              : "Screen connection closed. Remote input and video have stopped."
          );
          connection.ontrack = null;
          connection.close();
          if (peer === connection) peer = null;
        }
      };

      connection.oniceconnectionstatechange = () => {
        if (connection.iceConnectionState === "failed") {
          report("Local network could not establish the screen connection.");
        }
      };

      await connection.setRemoteDescription({ type: "offer", sdp: offerSdp });
      const answer = await connection.createAnswer();
      await connection.setLocalDescription(answer);
      await waitForIceGatheringComplete(connection);

      if (!connection.localDescription || !connection.localDescription.sdp) {
        throw new Error("The phone could not create a WebRTC answer.");
      }

      if (!window.Fasx || typeof window.Fasx.submitAnswer !== "function") {
        throw new Error("The secure FASX Android bridge is unavailable.");
      }

      window.Fasx.submitAnswer(connection.localDescription.sdp);
      report("Secure answer sent. Establishing the local screen connection…");
    } catch (error) {
      report("Could not start the view-only stream: " + (error.message || "Unknown error"));
      stopFasxRemoteView(false);
    }
  }

  function startConnectionStats(connection) {
    stopConnectionStats();
    const sample = async () => {
      if (peer !== connection || connection.connectionState !== "connected") return;
      try {
        const reportSet = await connection.getStats();
        let videoStats = null;
        let roundTripSeconds = null;
        for (const item of reportSet.values()) {
          if (item.type === "inbound-rtp" && (item.kind === "video" || item.mediaType === "video")) {
            videoStats = item;
          }
          if (item.type === "candidate-pair" && item.state === "succeeded" && item.nominated) {
            if (typeof item.currentRoundTripTime === "number") {
              roundTripSeconds = item.currentRoundTripTime;
            }
          }
        }

        if (videoStats) {
          const now = performance.now();
          const bytes = Number(videoStats.bytesReceived || 0);
          const frames = Number(videoStats.framesDecoded || 0);
          let bitrate = null;
          let frameRate = null;
          if (previousVideoStats && now > previousVideoStats.at) {
            const seconds = (now - previousVideoStats.at) / 1000;
            bitrate = Math.max(0, ((bytes - previousVideoStats.bytes) * 8) / seconds / 1000000);
            frameRate = Math.max(0, (frames - previousVideoStats.frames) / seconds);
          }
          previousVideoStats = { at: now, bytes, frames };

          const dimensions = videoStats.frameWidth && videoStats.frameHeight
            ? videoStats.frameWidth + "×" + videoStats.frameHeight
            : "resolution pending";
          const fps = frameRate === null ? "measuring…" : frameRate.toFixed(1) + " fps";
          const mbps = bitrate === null ? "measuring…" : bitrate.toFixed(2) + " Mbps";
          const rtt = roundTripSeconds === null ? "RTT unavailable" : Math.round(roundTripSeconds * 1000) + " ms RTT";
          metrics.textContent = dimensions + " • " + fps + " • " + mbps + " • " + rtt;
        }
      } catch (_) {
        metrics.textContent = "Connection statistics unavailable";
      }
    };

    statsTimer = setInterval(sample, 2000);
    sample();
  }

  function stopConnectionStats() {
    if (statsTimer !== null) {
      clearInterval(statsTimer);
      statsTimer = null;
    }
    previousVideoStats = null;
    metrics.textContent = "Connection metrics appear during an active stream.";
  }

  function stopFasxRemoteView(updateStatus = true) {
    stopConnectionStats();
    if (inputChannel) {
      inputChannel.onopen = null;
      inputChannel.onclose = null;
      inputChannel.close();
      inputChannel = null;
    }
    if (peer) {
      peer.ontrack = null;
      peer.onconnectionstatechange = null;
      peer.close();
      peer = null;
    }
    video.srcObject = null;
    controls.hidden = true;
    video.style.touchAction = "pan-y";
    controlAllowed = false;
    pointerDown = null;
    if (updateStatus) report("View-only connection stopped.");
  }

  video.addEventListener("pointerdown", (event) => {
    if (!controlAllowed || !inputChannel || inputChannel.readyState !== "open") return;
    const point = toScreenPoint(event.clientX, event.clientY);
    if (!point) return;
    pointerDown = { id: event.pointerId, x: event.clientX, y: event.clientY, moved: false, point };
    try { video.setPointerCapture(event.pointerId); } catch (_) { /* optional */ }
    event.preventDefault();
  });

  video.addEventListener("pointermove", (event) => {
    if (!pointerDown || pointerDown.id !== event.pointerId) return;
    const point = toScreenPoint(event.clientX, event.clientY);
    if (!point) return;
    if (Math.abs(event.clientX - pointerDown.x) > 3 || Math.abs(event.clientY - pointerDown.y) > 3) {
      pointerDown.moved = true;
    }
    const now = Date.now();
    if (now - lastMoveAt >= 35) {
      sendInput({ kind: "pointer", action: "move", x: point.x, y: point.y });
      lastMoveAt = now;
    }
    event.preventDefault();
  });

  video.addEventListener("pointerup", (event) => {
    if (!pointerDown || pointerDown.id !== event.pointerId) return;
    const point = toScreenPoint(event.clientX, event.clientY) || pointerDown.point;
    if (!pointerDown.moved) {
      sendInput({ kind: "pointer", action: "click", x: point.x, y: point.y });
    } else {
      sendInput({ kind: "pointer", action: "move", x: point.x, y: point.y });
    }
    pointerDown = null;
    event.preventDefault();
  });

  video.addEventListener("pointercancel", () => { pointerDown = null; });

  video.addEventListener("contextmenu", (event) => {
    if (!controlAllowed) return;
    event.preventDefault();
    const point = toScreenPoint(event.clientX, event.clientY);
    if (point) sendInput({ kind: "pointer", action: "rightClick", x: point.x, y: point.y });
  });

  video.addEventListener("wheel", (event) => {
    if (!controlAllowed) return;
    const point = toScreenPoint(event.clientX, event.clientY);
    if (!point) return;
    const delta = Math.max(-120, Math.min(120, Math.round(-event.deltaY)));
    if (delta !== 0) sendInput({ kind: "pointer", action: "scroll", x: point.x, y: point.y, delta });
    event.preventDefault();
  }, { passive: false });

  document.querySelectorAll("[data-key]").forEach(button => {
    button.addEventListener("click", () => {
      sendInput({ kind: "key", action: "press", key: button.getAttribute("data-key") });
    });
  });

  sendTextButton.addEventListener("click", () => {
    const value = textInput.value;
    if (!value || value.length > 160 || /[\u0000-\u001f\u007f]/.test(value)) return;
    if (sendInput({ kind: "text", action: "type", text: value })) textInput.value = "";
  });

  textInput.addEventListener("keydown", event => {
    if (event.key === "Enter") {
      event.preventDefault();
      sendTextButton.click();
    }
  });

  window.startFasxRemoteView = startFasxRemoteView;
  window.stopFasxRemoteView = stopFasxRemoteView;
})();