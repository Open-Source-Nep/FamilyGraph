import { NavigationError } from '@angular/router';

import { reloadOnStaleChunk } from './stale-chunk';

describe('reloadOnStaleChunk', () => {
  const stale = new TypeError(
    'Failed to fetch dynamically imported module: https://example.org/chunk-4O3CN8Q5.js',
  );

  function memoryStorage() {
    const values = new Map<string, string>();
    return {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => void values.set(key, value),
    };
  }

  it('loads the target page in full when its chunk is gone', () => {
    const location = { assign: vi.fn() };

    reloadOnStaleChunk(new NavigationError(1, '/people', stale), location, memoryStorage(), 1000);

    expect(location.assign).toHaveBeenCalledWith('/people');
  });

  it('reloads only once a minute, so a real failure is not a reload loop', () => {
    const location = { assign: vi.fn() };
    const storage = memoryStorage();

    reloadOnStaleChunk(new NavigationError(1, '/people', stale), location, storage, 1000);
    reloadOnStaleChunk(new NavigationError(2, '/people', stale), location, storage, 30_000);
    reloadOnStaleChunk(new NavigationError(3, '/people', stale), location, storage, 70_000);

    expect(location.assign).toHaveBeenCalledTimes(2);
  });

  it('leaves other navigation errors alone', () => {
    const location = { assign: vi.fn() };

    reloadOnStaleChunk(
      new NavigationError(1, '/people', new Error('Guard failed')),
      location,
      memoryStorage(),
      1000,
    );

    expect(location.assign).not.toHaveBeenCalled();
  });
});
