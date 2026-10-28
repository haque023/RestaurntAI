import type { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonVariant = "default" | "primary" | "danger" | "ghost";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: "md" | "sm";
  busy?: boolean;
}

export function Button({ variant = "default", size = "md", busy = false, className, children, disabled, type = "button", ...rest }: ButtonProps) {
  const classes = ["btn", variant !== "default" && `btn-${variant}`, size === "sm" && "btn-sm", className].filter(Boolean).join(" ");
  return (
    <button type={type} className={classes} disabled={disabled || busy} aria-busy={busy || undefined} {...rest}>
      {busy && <span className="spinner" aria-hidden="true" style={{ width: 14, height: 14 }} />}
      {children}
    </button>
  );
}

export function Card({ children, narrow = false, as: Tag = "section" }: { children: ReactNode; narrow?: boolean; as?: "section" | "article" | "div" }) {
  return <Tag className={narrow ? "card card-narrow" : "card"}>{children}</Tag>;
}

export function PageHeader({ title, description, actions }: { title: string; description?: ReactNode; actions?: ReactNode }) {
  return (
    <header className="page-header">
      <div>
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      {actions && <div className="row">{actions}</div>}
    </header>
  );
}

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
    </label>
  );
}

export type BadgeTone = "neutral" | "success" | "warning" | "danger";

export function Badge({ tone = "neutral", children }: { tone?: BadgeTone; children: ReactNode }) {
  return <span className={tone === "neutral" ? "badge" : `badge badge-${tone}`}>{children}</span>;
}

export function Spinner({ label = "Loading" }: { label?: string }) {
  return (
    <div className="row muted" role="status">
      <span className="spinner" aria-hidden="true" />
      <span>{label}…</span>
    </div>
  );
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="empty">
      <h3>{title}</h3>
      {children && <div style={{ marginTop: 8 }}>{children}</div>}
    </div>
  );
}

interface ErrorLike {
  message?: string;
  correlationId?: string;
  status?: number;
}

/** Shows an API or network error, including the correlation id support needs to trace it. */
export function ErrorAlert({ error, title = "Something went wrong" }: { error: unknown; title?: string }) {
  if (!error) return null;
  const e = (typeof error === "object" ? error : { message: String(error) }) as ErrorLike;
  // 502-504 without problem details come from the dev proxy / edge when the gateway is down.
  const unreachable = e.message === "Failed to fetch" || (e.status !== undefined && e.status >= 502 && e.status <= 504 && !e.correlationId);
  const message = unreachable ? "Cannot reach the platform. Is the gateway running?" : e.message;

  return (
    <div className="alert alert-error" role="alert">
      <strong>{title}.</strong> {message}
      {e.correlationId && <div className="small mono">Reference: {e.correlationId}</div>}
    </div>
  );
}
