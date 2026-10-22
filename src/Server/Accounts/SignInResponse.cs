namespace Server.Accounts;

/// <summary>The local URL the Client goes to once sign-in has completed.</summary>
public sealed record SignInResponse(string ReturnUrl);
