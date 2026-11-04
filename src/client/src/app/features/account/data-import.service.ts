import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** What an import added (D-033, D-049); `reusedAccounts` matched an account here by Google login. */
export interface ImportResult {
  people: number;
  links: number;
  marriages: number;
  images: number;
  handovers: number;
  claims: number;
  auditEvents: number;
  createdAccounts: number;
  reusedAccounts: number;
}

/** One reason a file was refused, at `path` such as `people[3]`. */
export interface ImportProblem {
  path: string;
  code: string;
  message: string;
}

/** The administrator's data import. The server checks the role, the empty instance and the file. */
@Service()
export class DataImportService {
  private readonly http = inject(HttpClient);

  /** Sends the export zip as it is; the server reads and checks it whole. */
  import(file: Blob): Promise<ImportResult> {
    return firstValueFrom(
      this.http.post<ImportResult>('/api/import', file, {
        headers: { 'Content-Type': 'application/zip' },
      }),
    );
  }
}
