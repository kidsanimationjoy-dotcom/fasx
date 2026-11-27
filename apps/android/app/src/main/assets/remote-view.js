(() => {
  "use strict";

  const status = document.getElementById("status");
  const video = document.getElementById("remoteVideo");
  let peer = null;

  function report(message) {
    status.textContent = message;
    if (window.Fasx && typeof window.Fasx.reportStatus === "function") {
      window.Fasx.reportStatus(message);
    }
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

  async function startFasxRemoteView(offerSdp) {
    stopFasxRemoteView();
    report("Connecting to the approved Windows screen…");

    try {
      const connection = new RTCPeerConnection({ iceServers: [] });
      peer = connection;

      connection.addTransceiver("video", { direction: "recvonly" });

      connection.ontrack = async (event) => {
        const stream = event.streams && event.streams[0];
        if (stream) {
          video.srcObject = stream;
          try { await video.play(); } catch (_) { /* inline playback may need a user tap */ }
          report("Receiving the Windows screen. View-only mode is active.");
        }
      };

      connection.onconnectionstatechange = () => {
        const state = connection.connectionState;
        if (state === "connected") report("Connected. View-only screen stream is active.");
        else if (state === "disconnected") report("Screen connection interrupted.");
        else if (state === "failed") report("Screen connection failed. Stop the session and try again.");
        else if (state === "closed") report("Screen connection closed.");
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

  function stopFasxRemoteView(updateStatus = true) {
    if (peer) {
      peer.ontrack = null;
      peer.onconnectionstatechange = null;
      peer.close();
      peer = null;
    }
    video.srcObject = null;
    if (updateStatus) report("View-only connection stopped.");
  }

  window.startFasxRemoteView = startFasxRemoteView;
  window.stopFasxRemoteView = stopFasxRemoteView;
})();
