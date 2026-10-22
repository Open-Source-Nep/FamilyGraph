import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SignInPage } from './sign-in-page';

describe('SignInPage', () => {
  let fixture: ComponentFixture<SignInPage>;

  beforeEach(() => {
    fixture = TestBed.createComponent(SignInPage);
  });

  const link = () => fixture.nativeElement.querySelector('a') as HTMLAnchorElement;

  it('signs in with Google and returns to the requested page', async () => {
    fixture.componentRef.setInput('returnUrl', '/account');
    await fixture.whenStable();

    expect(link().getAttribute('href')).toBe('/api/account/sign-in?returnUrl=%2Faccount');
    expect(fixture.nativeElement.querySelector('[role=alert]')).toBeNull();
  });

  it('returns to the home page by default', async () => {
    await fixture.whenStable();

    expect(link().getAttribute('href')).toBe('/api/account/sign-in?returnUrl=%2F');
  });

  it('says when sign-in failed', async () => {
    fixture.componentRef.setInput('failed', 'true');
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('[role=alert]')).not.toBeNull();
  });
});
