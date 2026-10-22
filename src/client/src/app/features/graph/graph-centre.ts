/**
 * The last person a graph was centred on, kept in this browser only so the home page can show the
 * same graph again (D-024). It is an opaque person ID; the server still checks every read. Storage
 * can be blocked or cleared, so every access is allowed to fail.
 */
const key = 'familygraph.graphCentre';

export function rememberedCentre(): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function rememberCentre(personId: string): void {
  try {
    localStorage.setItem(key, personId);
  } catch {
    // Without storage, home falls back to your first profile.
  }
}

export function forgetCentre(): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // Nothing to forget.
  }
}
