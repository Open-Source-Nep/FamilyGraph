import { Pipe, PipeTransform } from '@angular/core';

import { localDateTime, localNumber } from './local-format';

/** `{{ count | digits }}`: a number in the page's language (D-047). */
@Pipe({ name: 'digits' })
export class DigitsPipe implements PipeTransform {
  transform(value: number): string {
    return localNumber(value);
  }
}

/** `{{ expiresAt | localDateTime }}`: an ISO moment as a date and time in the page's language. */
@Pipe({ name: 'localDateTime' })
export class LocalDateTimePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return value ? localDateTime(value) : '';
  }
}
