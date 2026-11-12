# nginx (optional)

Placeholder for a self-hosted TLS-terminating reverse proxy in front of the YARP gateway,
for environments that do not yet use a managed edge (Cloudflare, Azure Front Door, AWS CloudFront).

```
Internet → [nginx | managed CDN/WAF] → Gateway.Api (YARP) → business services
```

The gateway is edge-ready: it honours `X-Forwarded-*` headers. When an edge is introduced,
configure `KnownProxies`/`KnownIPNetworks` in `ReverseProxyConfig` so only the edge is trusted.
See `nginx.conf` for a minimal starting point.
