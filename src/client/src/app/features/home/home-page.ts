import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-home-page',
  imports: [RouterLink],
  template: `
    <section class="panel">
      <h1>FamilyGraph</h1>
      <p>Signed in as {{ account()?.email }}.</p>
      <p>The family graph will appear here.</p>
      <p><a routerLink="/people">Profiles you maintain</a></p>
    </section>
  `,
})
export class HomePage {
  protected readonly account = inject(AuthService).account;
}
