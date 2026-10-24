import { NavigationError } from '@angular/router';

/** Browsers word a missing lazy-loaded file differently: Chromium, Safari and Firefox. */
const missingChunk =
  /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module/i;

const reloadedKey = 'familygraph.stale-chunk-reload';

/**
 * A tab opened before a deploy still runs the previous build, whose lazy pages name chunk files the
 * new build no longer has. Loading the target URL in full fetches the current build. A second
 * failure within a minute is a real error and is left to the router.
 */
export function reloadOnStaleChunk(
  error: NavigationError,
  location: Pick<Location, 'assign'> = window.location,
  storage: Pick<Storage, 'getItem' | 'setItem'> = sessionStorage,
  now: number = Date.now(),
): void {
  if (!missingChunk.test(String(error.error?.message ?? error.error))) {
    return;
  }
  const last = read(storage);
  if (last !== null && now - Number(last) < 60_000) {
    return;
  }
  write(storage, String(now));
  location.assign(error.url);
}

function read(storage: Pick<Storage, 'getItem'>): string | null {
  try {
    return storage.getItem(reloadedKey);
  } catch {
    return null;
  }
}

function write(storage: Pick<Storage, 'setItem'>, value: string): void {
  try {
    storage.setItem(reloadedKey, value);
  } catch {
    // Without storage the guard is lost, but a reload still fetches the current build.
  }
}
