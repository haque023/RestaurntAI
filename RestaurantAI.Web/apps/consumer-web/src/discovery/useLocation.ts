import { useCallback, useState } from "react";

export interface Coordinates {
  lat: number;
  lng: number;
}

type LocationState =
  | { status: "off" }
  | { status: "locating" }
  | { status: "on"; coords: Coordinates }
  | { status: "error"; message: string };

/** Browser geolocation for "near me". Only requested when the guest asks for it. */
export function useNearMe() {
  const [state, setState] = useState<LocationState>({ status: "off" });

  const locate = useCallback(() => {
    if (!("geolocation" in navigator)) {
      setState({ status: "error", message: "This browser cannot share your location." });
      return;
    }
    setState({ status: "locating" });
    navigator.geolocation.getCurrentPosition(
      (position) => setState({ status: "on", coords: { lat: position.coords.latitude, lng: position.coords.longitude } }),
      (error) =>
        setState({
          status: "error",
          message: error.code === error.PERMISSION_DENIED ? "Location permission was denied." : "Your location could not be determined.",
        }),
      { enableHighAccuracy: false, timeout: 10_000, maximumAge: 300_000 },
    );
  }, []);

  const clear = useCallback(() => setState({ status: "off" }), []);

  return { state, coords: state.status === "on" ? state.coords : null, locate, clear };
}
