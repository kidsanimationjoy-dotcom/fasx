(() => {
  "use strict";

  const startButton = document.getElementById("startButton");
  const stopButton = document.getElementById("stopButton");
  const status = document.getElementById("status");
  const preview = document.getElementById("preview");

  let activeStream = null;

  function setStatus(message) {
    status.textContent = message;
    if (window.chrome?.webview) {
      window.chrome.webview.postMessage(message === "Screen capture is active."
        ? "captureStarted"
        : message === "Screen capture stopped."
          ? "captureStopped"
          : message);
    }
  }

  function stopCapture() {
    if (activeStream) {
      for (const track of activeStream.getTracks()) {
        track.stop();
      }
      activeStream = null;
    }

    preview.srcObject = null;
    startButton.disabled = false;
    stopButton.disabled = true;
    setStatus("Screen capture stopped.");
  }

  async function startCapture() {
    if (!navigator.mediaDevices?.getDisplayMedia) {
      setStatus("This WebView2 runtime does not expose the Screen Capture API.");
      return;
    }

    startButton.disabled = true;
    setStatus("Waiting for the Windows screen picker...");

    try {
      const stream = await navigator.mediaDevices.getDisplayMedia({
        video: { frameRate: { ideal: 15, max: 30 } },
        audio: false
      });

      const videoTrack = stream.getVideoTracks()[0];
      if (!videoTrack) {
        for (const track of stream.getTracks()) track.stop();
        throw new Error("Windows did not return a video track.");
      }

      activeStream = stream;
      preview.srcObject = stream;
      await preview.play();

      videoTrack.addEventListener("ended", () => {
        if (activeStream === stream) stopCapture();
      }, { once: true });

      stopButton.disabled = false;
      setStatus("Screen capture is active.");
    } catch (error) {
      startButton.disabled = false;
      stopButton.disabled = true;

      if (error?.name === "NotAllowedError") {
        setStatus("Screen capture was cancelled or not allowed. Nothing is being shared.");
      } else {
        setStatus("Could not start screen capture: " + (error?.message || "Unknown error"));
      }
    }
  }

  startButton.addEventListener("click", startCapture);
  stopButton.addEventListener("click", stopCapture);
  window.fasxStopCapture = stopCapture;

  setStatus("Capture is off. Nothing is being shared.");
})();
