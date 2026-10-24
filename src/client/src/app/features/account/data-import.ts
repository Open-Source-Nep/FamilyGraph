import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';

import { DataImportService, ImportProblem, ImportResult } from './data-import.service';

/** Wording for the import's refusals (D-033). */
const messages: Record<string, string> = {
  'import.not_administrator': 'Only an administrator can import data.',
  'import.instance_not_empty':
    'This instance already has profiles. Data can only be imported into an empty instance.',
  'import.invalid_file': 'The file cannot be imported. Nothing was saved.',
};

/** The account page's "Data import" box for administrators: upload an export file (D-033). */
@Component({
  selector: 'app-data-import',
  templateUrl: './data-import.html',
})
export class DataImport {
  private readonly imports = inject(DataImportService);

  protected readonly file = signal<File | null>(null);
  protected readonly importing = signal(false);
  protected readonly result = signal<ImportResult | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly problems = signal<readonly ImportProblem[]>([]);
  protected readonly moreProblems = signal(false);

  protected choose(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
    this.result.set(null);
    this.error.set(null);
    this.problems.set([]);
    this.moreProblems.set(false);
  }

  protected async import(): Promise<void> {
    const file = this.file();
    if (!file) {
      return;
    }

    this.importing.set(true);
    this.error.set(null);
    this.problems.set([]);
    this.moreProblems.set(false);
    try {
      this.result.set(await this.imports.import(file));
    } catch (error) {
      this.showError(error);
    } finally {
      this.importing.set(false);
    }
  }

  private showError(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 413) {
        this.error.set('This file is too large to import.');
        return;
      }
      const code: unknown = error.error?.code;
      if (typeof code === 'string' && code in messages) {
        this.error.set(messages[code]);
        this.problems.set(Array.isArray(error.error?.problems) ? error.error.problems : []);
        this.moreProblems.set(error.error?.moreProblems === true);
        return;
      }
    }
    this.error.set('Something went wrong. Please try again.');
  }
}
