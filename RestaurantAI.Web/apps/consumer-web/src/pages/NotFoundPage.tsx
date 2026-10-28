import { EmptyState } from "@restaurant-ai/ui";
import { Link } from "react-router";

export function NotFoundPage() {
  return (
    <EmptyState title="Page not found">
      <Link to="/">Go home</Link>
    </EmptyState>
  );
}
