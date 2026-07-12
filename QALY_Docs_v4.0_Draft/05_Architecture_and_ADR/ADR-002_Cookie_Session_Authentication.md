# ADR-002: Same-Origin Cookie Session Authentication

Status: Accepted for v4.0 target
Supersedes: v3.2 browser access-token and refresh-token wording

## Context

The current browser application uses ASP.NET cookie authentication with a Redis ticket store and in-memory fallback. The v3.2 contract describes bearer access and refresh tokens. Maintaining both as implicit requirements creates false acceptance failures and unnecessary credential exposure in browser code.

## Decision

The Qaly web client uses a same-origin server-managed cookie session.

- Authentication cookies are HttpOnly, Secure in production, and SameSite according to the deployment topology.
- State-changing requests use CSRF protection.
- Session tickets are stored server-side so logout and revoke can invalidate them.
- Redis is the production ticket dependency; degraded behavior is explicit and fail-fast.
- Browser JavaScript never reads or stores authentication credentials.
- External API clients are out of scope until a separate token-profile ADR is approved.

## Session behavior

1. Login validates credentials and creates a server-side ticket.
2. Current-user resolves the authenticated identity and authorized workspace context.
3. Sliding renewal preserves a valid active session within configured limits.
4. Logout deletes the current ticket and cookie.
5. Revoke-all deletes other tickets for the user and records an audit event.
6. Redis degradation reports health state and must not add repeated multi-second timeout to every request.

## Security requirements

- Rate limit and audit failed login without logging secrets.
- Rotate session identifiers after authentication and privilege changes.
- Apply absolute and idle expiry.
- Validate Origin or anti-forgery token for state-changing browser requests.
- Keep fallback tickets process-local, bounded, observable, and disabled or explicitly approved for production.

## Consequences

The web application has a simpler secure credential model and matches current implementation direction. Non-browser integrations cannot assume this contract and require a future ADR.

## Acceptance evidence

- Integration tests cover login, current user, renewal, logout, revoke, expiry, and CSRF.
- Fault injection proves Redis-unavailable behavior is fail-fast and labeled degraded.
- Production configuration evidence proves cookie flags and secret isolation.
