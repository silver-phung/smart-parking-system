import { useEffect, useState } from "react";
import { apiRequest } from "./api.js";

const databaseImage = /^\/api\/v1\/images\/content\/[a-f0-9]{32}$/;

export function useImageUrl(url) {
  const [image, setImage] = useState({ url: null, source: null, error: false });
  useEffect(() => {
    if (!databaseImage.test(url || "")) return undefined;
    const controller = new AbortController();
    let objectUrl;
    setImage({ url, source: null, error: false });
    apiRequest(url.slice(4), { signal: controller.signal, responseType: "blob" })
      .then((blob) => {
        if (controller.signal.aborted) return;
        objectUrl = URL.createObjectURL(blob);
        setImage({ url, source: objectUrl, error: false });
      })
      .catch(() => {
        if (!controller.signal.aborted) setImage({ url, source: null, error: true });
      });
    return () => {
      controller.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [url]);

  if (databaseImage.test(url || "")) return image.url === url ? image : { source: null, error: false };
  const valid = /^(https?:\/\/|blob:)/i.test(url || "");
  return { source: valid ? url : null, error: Boolean(url && !valid) };
}
