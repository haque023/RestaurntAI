import { useApi } from "@restaurant-ai/auth";
import { useQueryClient } from "@tanstack/react-query";
import { AssistantChat } from "../../shared/AssistantChat";

const SUGGESTIONS = ["What do you recommend?", "Something spicy under 500", "What's vegan here?", "Book a table for 4 tomorrow at 8pm"];

/** Conversational AI waiter for one branch. Prices and availability always come from live tools. */
export function WaiterChat({ branchId, tableId }: { branchId: string; tableId: string | null }) {
  const api = useApi();
  const queryClient = useQueryClient();

  return (
    <AssistantChat
      title="AI waiter"
      openLabel="Ask the AI waiter"
      intro="Ask about dishes, ingredients or allergies, get recommendations, order, book a table, or ask for a member of staff. Any language is fine."
      suggestions={SUGGESTIONS}
      storageKey={`rai.consumer.waiter.${branchId}`}
      send={(conversationId, message) => api.ai.waiterTurn({ conversationId, branchId, tableId, message })}
      onReply={() => {
        // The waiter may have placed an order, made a reservation or called staff.
        void queryClient.invalidateQueries({ queryKey: ["menu", branchId] });
        void queryClient.invalidateQueries({ queryKey: ["my-orders"] });
        void queryClient.invalidateQueries({ queryKey: ["my-requests"] });
      }}
    />
  );
}
