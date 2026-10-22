/** Instance settings from `GET /api/configuration`. It carries no secrets. */
export interface ClientConfiguration {
  google: { enabled: boolean };
}
