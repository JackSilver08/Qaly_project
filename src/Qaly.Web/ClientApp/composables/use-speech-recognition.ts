import { ref } from "vue";

export function useSpeechRecognition(onResultCallback: (text: string, isFinal: boolean) => void) {
  const isSupported = ref(
    typeof window !== "undefined" &&
      ("SpeechRecognition" in window || "webkitSpeechRecognition" in window)
  );
  const isListening = ref(false);
  let recognition: any = null;
  let shouldBeActive = false;

  function start(lang: string = "vi-VN") {
    if (!isSupported.value) return;

    shouldBeActive = true;
    const SpeechRecognition =
      (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;

    if (!recognition) {
      recognition = new SpeechRecognition();
      recognition.continuous = true;
      recognition.interimResults = true;
      recognition.lang = lang;

      recognition.onstart = () => {
        isListening.value = true;
      };

      recognition.onend = () => {
        isListening.value = false;
        // Auto-restart if we expect it to be active
        if (shouldBeActive) {
          try {
            recognition.start();
          } catch (e) {
            console.warn("Failed to auto-restart speech recognition:", e);
          }
        }
      };

      recognition.onresult = (event: any) => {
        let interimText = "";
        let finalText = "";
        for (let i = event.resultIndex; i < event.results.length; ++i) {
          if (event.results[i].isFinal) {
            finalText += event.results[i][0].transcript;
          } else {
            interimText += event.results[i][0].transcript;
          }
        }

        if (finalText) {
          onResultCallback(finalText.trim(), true);
        } else if (interimText) {
          onResultCallback(interimText.trim(), false);
        }
      };
    }

    try {
      recognition.start();
    } catch (e) {
      // Speech recognition might already be running
      console.warn("Speech recognition start attempt:", e);
    }
  }

  function stop() {
    shouldBeActive = false;
    if (recognition) {
      try {
        recognition.stop();
      } catch (e) {
        console.warn("Speech recognition stop attempt:", e);
      }
    }
    isListening.value = false;
  }

  return {
    isSupported,
    isListening,
    start,
    stop,
  };
}
