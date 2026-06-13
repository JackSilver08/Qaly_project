import { ref } from "vue";

export interface RecoveryTranscriptEntry {
  senderName: string;
  text: string;
  timestamp: number;
}

const DB_NAME = "QalyMeetRecoveryDB";
const STORE_NAME = "transcripts";
const DB_VERSION = 1;

function openDB(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME);
      }
    };
  });
}

export function useMeetingRecovery() {
  async function saveBuffer(meetingId: string, entries: RecoveryTranscriptEntry[]) {
    try {
      const db = await openDB();
      const transaction = db.transaction(STORE_NAME, "readwrite");
      const store = transaction.objectStore(STORE_NAME);

      // Filter entries within the last 3 minutes (180,000 ms)
      const threeMinutesAgo = Date.now() - 3 * 60 * 1000;
      const rollingEntries = entries.filter((e) => e.timestamp >= threeMinutesAgo);

      store.put(rollingEntries, meetingId);

      return new Promise<void>((resolve, reject) => {
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
      });
    } catch (e) {
      console.warn("Failed to save transcript to IndexedDB:", e);
    }
  }

  async function getBuffer(meetingId: string): Promise<RecoveryTranscriptEntry[]> {
    try {
      const db = await openDB();
      const transaction = db.transaction(STORE_NAME, "readonly");
      const store = transaction.objectStore(STORE_NAME);
      const request = store.get(meetingId);

      return new Promise<RecoveryTranscriptEntry[]>((resolve, reject) => {
        request.onsuccess = () => {
          const result = request.result || [];
          // Filter out entries older than 3 minutes just in case
          const threeMinutesAgo = Date.now() - 3 * 60 * 1000;
          resolve(result.filter((e: any) => e.timestamp >= threeMinutesAgo));
        };
        request.onerror = () => reject(request.error);
      });
    } catch (e) {
      console.warn("Failed to get transcript from IndexedDB:", e);
      return [];
    }
  }

  async function clearBuffer(meetingId: string) {
    try {
      const db = await openDB();
      const transaction = db.transaction(STORE_NAME, "readwrite");
      const store = transaction.objectStore(STORE_NAME);
      store.delete(meetingId);

      return new Promise<void>((resolve, reject) => {
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
      });
    } catch (e) {
      console.warn("Failed to clear IndexedDB buffer:", e);
    }
  }

  return {
    saveBuffer,
    getBuffer,
    clearBuffer,
  };
}
