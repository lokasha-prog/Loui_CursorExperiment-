# ADR-0002: SeaweedFS replaces MinIO in local development
- Status: Accepted (routine technical decision, 2026-10-20)
- Context: MinIO no longer publishes free community container images (Docker Hub and Quay pulls fail).
- Decision: Local dev uses SeaweedFS S3 (port 9000 -> 8333). Application code uses the S3 API only, so on-prem MinIO or any S3-compatible store remains a deployment choice to revisit before Sprint 5.
- Consequences: No MinIO console on :9001; SeaweedFS master UI is on :9333.
