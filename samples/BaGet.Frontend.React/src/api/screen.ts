import { routeFor } from '../routes';

export type Status = 'Ok' | 'Invalid' | 'NotFound' | 'Forbidden' | 'SignIn' | 'Next';
const names: Status[] = ['Ok', 'Invalid', 'NotFound', 'Forbidden', 'SignIn', 'Next'];

export interface ScreenNext {
  screen?: { controller?: string | null; action?: string | null } | null;
  routeValues?: Record<string, unknown> | null;
  message?: string | null;
  error?: string | null;
  localUrl?: string | null;
  fragment?: string | null;
}

/** The shape of every /api/ui answer. `model` is typed per endpoint by the generated client. */
export interface ScreenResultLike<T> {
  status?: unknown;
  model?: T | null;
  errors?: { [field: string]: string[] } | null;
  next?: ScreenNext | null;
  statusCode?: unknown;
}

/** What the screens work with: a normalised status, the HTTP status and the typed model. */
export interface Screen<T> {
  status: Status;
  http: number;
  model: T | null;
  errors: Record<string, string[]>;
  next: ScreenNext | null;
}

/**
 * Turns an openapi-fetch answer into a `Screen`. Only 200 is described in the OpenAPI document, so
 * the 400/401/403/404 bodies (which are ScreenResults too) arrive in `error`, untyped; they are read
 * here once. The wire sends the status by name; the document says integer, so both are accepted.
 */
export function toScreen<T>(r: { data?: ScreenResultLike<T>; error?: unknown; response: Response }): Screen<T> {
  const body = (r.data ?? r.error ?? {}) as ScreenResultLike<T>;
  const http = r.response.status;
  let status: Status | undefined;
  if (typeof body.status === 'string' && (names as string[]).includes(body.status)) status = body.status as Status;
  else if (typeof body.status === 'number') status = names[body.status];
  status ??= http === 401 ? 'SignIn' : http === 403 ? 'Forbidden' : http === 404 ? 'NotFound' : http === 400 ? 'Invalid' : 'Ok';
  return { status, http, model: body.model ?? null, errors: body.errors ?? {}, next: body.next ?? null };
}

/** The message of a refusal: the form-level error, else a generic text for the status. */
export function refusalText(s: Screen<unknown>): string {
  const form = s.errors[''];
  if (form?.length) return form.join(' ');
  if (s.status === 'SignIn') return 'Sign in to see this.';
  if (s.status === 'Forbidden') return 'You may not see this.';
  if (s.status === 'NotFound') return 'Not found.';
  return 'The server refused the request (HTTP ' + s.http + ').';
}

/** The SPA route a successful write sends the user to, or undefined when it stays here. */
export function nextRoute(s: Screen<unknown>): string | undefined {
  return s.next?.screen ? routeFor(s.next.screen, s.next.routeValues ?? {}) : undefined;
}
