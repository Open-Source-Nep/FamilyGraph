import { Component, input, output, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { rememberCentre } from '../graph/graph-centre';
import { GraphView } from '../graph/graph-view';
import { PeopleService } from '../people/people.service';
import { PersonSummary } from '../people/person';
import { HomePage } from './home-page';

/** The graph itself is covered by the GraphView tests. */
@Component({ selector: 'app-graph-view', template: '' })
class StubGraphView {
  readonly id = input.required<string>();
  readonly notFound = output<void>();
}

const profile = (id: string, firstName: string): PersonSummary => ({
  id,
  firstName,
  middleName: null,
  lastName: null,
});

describe('HomePage', () => {
  let fixture: ComponentFixture<HomePage>;
  let profiles: PersonSummary[];

  beforeEach(() => {
    localStorage.clear();
    profiles = [profile('maya', 'Maya'), profile('gita', 'Gita')];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { account: signal({ email: 'a@example.org' }) } },
        {
          provide: PeopleService,
          useValue: {
            listMaintained: () => Promise.resolve({ people: profiles, isTruncated: false }),
          },
        },
      ],
    });
    TestBed.overrideComponent(HomePage, {
      remove: { imports: [GraphView] },
      add: { imports: [StubGraphView] },
    });
  });

  const show = async () => {
    fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();
  };
  const graph = () => fixture.debugElement.query((el) => el.name === 'app-graph-view');
  const centre = () => (graph()?.componentInstance as StubGraphView | undefined)?.id();

  it('centres on your first profile by default', async () => {
    await show();

    expect(centre()).toBe('maya');
  });

  it('centres on the person this browser last centred on', async () => {
    rememberCentre('sita');
    await show();

    expect(centre()).toBe('sita');
    expect(fixture.nativeElement.querySelector('select').value).toBe('');
  });

  it('falls back to your first profile when the remembered person is gone', async () => {
    rememberCentre('gone');
    await show();

    (graph().componentInstance as StubGraphView).notFound.emit();
    await fixture.whenStable();

    expect(centre()).toBe('maya');
    expect(localStorage.getItem('familygraph.graphCentre')).toBeNull();
  });

  it('switches to another of your profiles', async () => {
    await show();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    select.value = 'gita';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(centre()).toBe('gita');
  });

  it('asks for a profile when you have none', async () => {
    profiles = [];
    await show();

    expect(graph()).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('once you add a profile');
  });
});
