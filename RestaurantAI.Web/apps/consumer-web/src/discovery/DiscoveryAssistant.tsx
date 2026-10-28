import { useAuth } from "@restaurant-ai/auth";
import { Button, Card } from "@restaurant-ai/ui";
import { Link, useLocation } from "react-router";
import { AssistantChat } from "../assistant/AssistantChat";
import type { useNearMe } from "./useLocation";

const SUGGESTIONS = ["Spicy chicken under 400 near me", "Vegan dinner open now", "Best biryani nearby", "Somewhere quiet for 6 people"];

/**
 * "Ask AI" discovery: natural-language search across every listed restaurant, using the guest's
 * location when they share it. The assistant links each suggestion to that restaurant's menu.
 */
export function DiscoveryAssistant({ nearMe }: { nearMe: ReturnType<typeof useNearMe> }) {
  const { api, isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return (
      <Card>
        <div className="row row-between">
          <div>
            <h2>Ask AI what to eat</h2>
            <p className="muted small">Describe what you feel like, e.g. “spicy vegan under 300 near me”, and the assistant searches every restaurant.</p>
          </div>
          <Link className="btn btn-primary" to="/login" state={{ from: location.pathname + location.search }}>
            Sign in to ask
          </Link>
        </div>
      </Card>
    );
  }

  const coords = nearMe.coords;
  return (
    <AssistantChat
      title="Ask AI what to eat"
      openLabel="✨ Ask AI what to eat"
      startOpen
      intro="Tell me what you feel like (a dish, cuisine, diet, budget, distance) and I'll find matching restaurants and dishes."
      placeholder="e.g. something spicy and vegan under 300 near me"
      suggestions={SUGGESTIONS}
      storageKey="rai.consumer.discovery"
      toolbar={
        <div className="row small">
          {coords ? (
            <span className="muted">📍 Using your location</span>
          ) : (
            <Button size="sm" onClick={nearMe.locate} disabled={nearMe.state.status === "locating"}>
              {nearMe.state.status === "locating" ? "Locating…" : "📍 Use my location"}
            </Button>
          )}
          {nearMe.state.status === "error" && <span className="muted">{nearMe.state.message}</span>}
        </div>
      }
      send={(conversationId, message) =>
        api.ai.discoveryTurn({ conversationId, message, latitude: coords?.lat ?? null, longitude: coords?.lng ?? null })
      }
    />
  );
}
