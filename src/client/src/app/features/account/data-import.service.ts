import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** What an import added (D-033); `assignedToYou` profiles had no matching maintainer account. */
export interface ImportResult {
  people: number;
  links: number;
  marriages: number;
  matchedAccounts: number;
  assignedToYou: number;
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

  /** Sends the export file as it is; the server reads and checks it whole. */
  import(file: Blob): Promise<ImportResult> {
    return firstValueFrom(
      this.http.post<ImportResult>('/api/import', file, {
        headers: { 'Content-Type': 'application/json' },
      }),
    );
  }
}
