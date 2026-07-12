import { ref } from "vue";

export function useSpeechRecognition(onResultCallback: (text: string, isFinal: boolean) => void) {
  const isSupported = ref(
    typeof window !== "undefined" &&
      ("SpeechRecognition" in window || "webkitSpeechRecognition" in window)
  );
  const isListening = ref(false);
  const hasError = ref<string | null>(null);
  let recognition: any = null;
  let shouldBeActive = false;
  let restartTimeout: number | undefined;

  function start(lang: string = "vi-VN") {
    if (!isSupported.value) return;

    shouldBeActive = true;
    hasError.value = null;
    const SpeechRecognition =
      (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;

    if (!recognition) {
      recognition = new SpeechRecognition();
      recognition.continuous = true;
      recognition.interimResults = true;
      recognition.lang = lang;

      recognition.onstart = () => {
        isListening.value = true;
        hasError.value = null;
      };

      recognition.onerror = (event: any) => {
        console.warn("Speech recognition error:", event.error);
        if (event.error === 'not-allowed') {
          hasError.value = "Chưa cấp quyền Micro cho trình duyệt.";
          shouldBeActive = false;
        } else if (event.error === 'network') {
          hasError.value = "Lỗi mạng khi nhận diện giọng nói.";
        }
      };

      recognition.onend = () => {
        isListening.value = false;
        // Auto-restart if we expect it to be active
        if (shouldBeActive) {
          clearTimeout(restartTimeout);
          restartTimeout = window.setTimeout(() => {
            try {
              recognition.start();
            } catch (e) {
              console.warn("Failed to auto-restart speech recognition:", e);
            }
          }, 300);
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
      if (!isListening.value) {
        recognition.start();
      }
    } catch (e) {
      console.warn("Speech recognition start attempt:", e);
    }
  }

  function stop() {
    shouldBeActive = false;
    clearTimeout(restartTimeout);
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
    hasError,
    start,
    stop,
  };
}
