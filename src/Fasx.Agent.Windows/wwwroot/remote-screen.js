(() => {
  "use strict";

  const startButton = document.getElementById("startButton");
  const stopButton = document.getElementById("stopButton");
  const status = document.getElementById("status");
  const preview = document.getElementById("localPreview");

  let captureStream = null;
  let peer = null;
  let stopping = false;

  function report(type, message, extra = {}) {
    status.textContent = message;
    if (window.chrome?.webview) {
      window.chrome.webview.postMessage({ type, message, ...extra });
    }
  }

  function waitForIceGatheringComplete(connection, timeoutMs = 15000) {
    if (connection.iceGatheringState === "complete") return Promise.resolve();

    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        cleanup();
        reject(new Error("Local network candidate gathering timed out."));
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

  async function startRemoteScreen() {
    if (captureStream || peer) return;
    stopping = false;
    startButton.disabled = true;
    report("status", "Waiting for you to choose a screen in the Windows picker…");

    try {
      if (!navigator.mediaDevices?.getDisplayMedia) {
        throw new Error("This WebView2 runtime does not support the Screen Capture API.");
      }

      // The Windows picker is invoked directly from this button click.
      captureStream = await navigator.mediaDevices.getDisplayMedia({
        video: { frameRate: { ideal: 15, max: 20 } },
        audio: false
      });

      const track = captureStream.getVideoTracks()[0];
      if (!track) throw new Error("Windows did not return a display video track.");

      preview.srcObject = captureStream;
      try { await preview.play(); } catch (_) { /* local preview may be paused by WebView policy */ }

      peer = new RTCPeerConnection({ iceServers: [] });
      const connection = peer;

      connection.addTrack(track, captureStream);
      connection.onconnectionstatechange = () => {
        const state = connection.connectionState;
        report("peerState", "Phone connection: " + state, { state });

        if (state === "failed" || state === "closed") {
          stopRemoteScreen(true);
        }
      };

      connection.oniceconnectionstatechange = () => {
        if (connection.iceConnectionState === "failed") {
          report("status", "The phone could not be reached on this local network.");
          stopRemoteScreen(true);
        }
      };

      track.addEventListener("ended", () => stopRemoteScreen(true), { once: true });

      const offer = await connection.createOffer();
      await connection.setLocalDescription(offer);
      await waitForIceGatheringComplete(connection);

      if (!connection.localDescription?.sdp) {
        throw new Error("WebRTC did not produce a screen offer.");
      }

      stopButton.disabled = false;
      report("status", "Screen selected. Sending a view-only WebRTC offer to the approved phone.");
      window.chrome?.webview?.postMessage({
        type: "offer",
        sdp: connection.localDescription.sdp
      });
      report("captureStarted", "Local screen capture is active. The Windows capture border remains visible.");
    } catch (error) {
      startButton.disabled = false;
      stopButton.disabled = true;
      report("status", "Screen sharing did not start: " + (error?.message || "Unknown error"));
      stopRemoteScreen(false);
    }
  }

  async function applyAnswer(sdp) {
    if (!peer || !sdp || stopping) return;
    try {
      await peer.setRemoteDescription({ type: "answer", sdp });
      report("status", "Phone answer received. Establishing the view-only screen connection…");
    } catch (error) {
      report("status", "Could not apply the phone's WebRTC answer: " + (error?.message || "Unknown error"));
      stopRemoteScreen(true);
    }
  }

  function stopRemoteScreen(notifyHost = true) {
    if (stopping) return;
    stopping = true;

    const oldPeer = peer;
    peer = null;
    if (oldPeer) {
      oldPeer.onconnectionstatechange = null;
      oldPeer.oniceconnectionstatechange = null;
      try { oldPeer.close(); } catch (_) { /* already closed */ }
    }

    if (captureStream) {
      for (const track of captureStream.getTracks()) track.stop();
      captureStream = null;
    }

    preview.srcObject = null;
    startButton.disabled = false;
    stopButton.disabled = true;

    if (notifyHost) {
      report("captureStopped", "Screen sharing stopped. FASX will revoke View screen permission.");
    } else {
      status.textContent = "Screen capture is off. Nothing is being sent.";
    }

    stopping = false;
  }

  window.chrome?.webview?.addEventListener("message", event => {
    const message = event.data;
    if (message?.type === "answer") {
      applyAnswer(message.sdp);
    } else if (message?.type === "stop") {
      stopRemoteScreen(true);
    }
  });

  startButton.addEventListener("click", startRemoteScreen);
  stopButton.addEventListener("click", () => stopRemoteScreen(true));
  window.fasxStopRemoteScreen = () => stopRemoteScreen(false);

  report("status", "Ready. Press Start screen sharing to open the Windows picker.");
})();
