import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { imageUrl } from './person';

/**
 * Loads profile photos as data URLs for pictures drawn on a canvas, such as the graph diagram
 * (D-028). Each image is requested once per page load; an image the server no longer shows, for
 * example because the profile became private, resolves to null.
 */
@Service()
export class PhotoLoader {
  private readonly http = inject(HttpClient);
  private readonly loaded = new Map<string, Promise<string | null>>();

  dataUrl(person: { id: string; imageId: string | null }): Promise<string | null> {
    const url = imageUrl(person);
    if (!url) {
      return Promise.resolve(null);
    }
    let photo = this.loaded.get(url);
    if (!photo) {
      photo = firstValueFrom(this.http.get(url, { responseType: 'blob' })).then(
        readAsDataUrl,
        () => null,
      );
      this.loaded.set(url, photo);
    }
    return photo;
  }
}

function readAsDataUrl(blob: Blob): Promise<string | null> {
  return new Promise((resolve) => {
    const reader = new FileReader();
    reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : null);
    reader.onerror = () => resolve(null);
    reader.readAsDataURL(blob);
  });
}
