import { Component, inject } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-home-page',
  template: `
    <section class="panel">
      <h1>FamilyGraph</h1>
      <p>Signed in as {{ account()?.email }}.</p>
      <p>The family graph will appear here.</p>
    </section>
  `,
})
export class HomePage {
  protected readonly account = inject(AuthService).account;
}
