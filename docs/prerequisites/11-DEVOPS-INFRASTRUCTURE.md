# 11 — DevOps & Infrastructure

> Kubernetes, Terraform, CI/CD pipeline, environment strategy, observability stack, backup/restore.

---

## 1. Infrastructure Overview

### 1.1 SaaS Infrastructure (AWS UAE)

```
AWS Account (me-central-1 region)
├── VPC
│   ├── Public subnets (3 AZs) — ALB, NAT Gateway
│   ├── Private subnets (3 AZs) — EKS worker nodes
│   └── Database subnets (3 AZs) — RDS, ElastiCache
├── EKS Cluster (Kubernetes 1.30)
│   ├── Node groups (multi-AZ, auto-scaling 3-10 nodes)
│   ├── Namespaces: production, staging, dev, monitoring, keycloak
│   └── Workloads: NMS API, NMS Web, Keycloak, RabbitMQ, etc.
├── RDS PostgreSQL 16 (Multi-AZ, read replica)
├── ElastiCache Redis 7 (Multi-AZ)
├── Amazon MQ RabbitMQ 3.13 (Multi-AZ)
├── S3 Buckets (media, backups, terraform state, logs)
├── CloudFront (CDN with signed URLs)
├── Route 53 (DNS)
├── Secrets Manager (secrets)
├── KMS (encryption keys)
├── CloudWatch (logs, metrics, alarms)
└── WAF (on ALB)
```

### 1.2 On-Prem Infrastructure (Customer-Provided)

Customer provides one of:
- **Customer's cloud account** (AWS / Azure / GCP, UAE region preferred)
- **Customer's data center** with K8s cluster (self-managed or via Rancher/OpenShift)
- **Single-server deployment** (for small nurseries, via docker-compose — limited scale)

Our Terraform modules support AWS, Azure, and generic K8s targets. Same Helm charts deploy NMS to any K8s cluster.

---

## 2. Kubernetes Architecture

### 2.1 Cluster Sizing

| Phase | Worker nodes | vCPU per node | Memory per node | Total pods (approx) |
|---|---|---|---|---|
| Phase 1 (dev/staging) | 2 | 4 | 16 GB | 30 |
| Phase 1 (production) | 3 (min) - 10 (max) | 8 | 32 GB | 100 |
| Phase 3 (production) | 5 (min) - 20 (max) | 16 | 64 GB | 300 |

Auto-scaling: K8s Cluster Autoscaler + HPA (Horizontal Pod Autoscaler) on CPU + custom metrics.

### 2.2 Namespace Strategy

```
production       — NMS API, Web, Keycloak, RabbitMQ (production)
staging          — Same as production but smaller replicas
dev              — Shared dev environment
monitoring       — Prometheus, Grafana, Loki, Alertmanager
keycloak         — Keycloak (separate namespace for security isolation)
infra            — Ingress controller, cert-manager, external-dns
```

### 2.3 Pod Resources

| Workload | CPU req | CPU limit | Memory req | Memory limit | Replicas (prod) |
|---|---|---|---|---|---|
| NMS API | 500m | 2000m | 1 Gi | 4 Gi | 3 (HPA 3-10) |
| NMS Web (nginx serving static) | 100m | 500m | 128 Mi | 256 Mi | 2 |
| Keycloak | 1000m | 2000m | 2 Gi | 4 Gi | 2 |
| RabbitMQ | 500m | 1000m | 1 Gi | 2 Gi | 3 (cluster) |
| Hangfire workers | 500m | 2000m | 1 Gi | 2 Gi | 2 |
| Media Processing consumer | 500m | 2000m | 1 Gi | 2 Gi | 2 |
| Prometheus | 500m | 1000m | 2 Gi | 4 Gi | 1 |
| Grafana | 100m | 500m | 256 Mi | 512 Mi | 1 |
| Loki | 500m | 1000m | 2 Gi | 4 Gi | 1 |

### 2.4 HPA (Horizontal Pod Autoscaler)

NMS API HPA:
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: nms-api
  namespace: production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: nms-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  - type: Pods
    pods:
      metric:
        name: http_requests_per_second
      target:
        type: AverageValue
        averageValue: "100"
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 30
      policies:
      - type: Percent
        value: 100
        periodSeconds: 30
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
```

### 2.5 Pod Disruption Budgets

Ensure minimum availability during node drains:
```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: nms-api
  namespace: production
spec:
  minAvailable: 2
  selector:
    matchLabels:
      app: nms-api
```

### 2.6 Network Policies

Default deny, allow explicitly:
```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: nms-api
  namespace: production
spec:
  podSelector:
    matchLabels:
      app: nms-api
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: infra
      podSelector:
        matchLabels:
          app: ingress-nginx
    ports:
    - protocol: TCP
      port: 8080
  egress:
  - to:
    - namespaceSelector:
        matchLabels:
          name: production
      podSelector:
        matchLabels:
          app: postgres
    ports:
    - protocol: TCP
      port: 5432
  - to:
    - namespaceSelector:
        matchLabels:
          name: production
      podSelector:
        matchLabels:
          app: redis
    ports:
    - protocol: TCP
      port: 6379
  # ... etc
```

---

## 3. Terraform Infrastructure-as-Code

### 3.1 Structure

```
infra/terraform/
├── modules/
│   ├── vpc/                   # VPC, subnets, NAT gateway, IGW
│   ├── eks/                   # EKS cluster, node groups, IRSA
│   ├── rds/                   # RDS PostgreSQL, parameter groups, secrets
│   ├── elasticache/           # ElastiCache Redis
│   ├── amazon-mq/             # Amazon MQ RabbitMQ
│   ├── s3/                    # S3 buckets with policies
│   ├── cloudfront/            # CloudFront distributions
│   ├── route53/               # DNS records
│   ├── kms/                   # KMS keys
│   ├── secrets_manager/       # Secrets Manager secrets
│   ├── waf/                   # WAF Web ACLs
│   └── monitoring/            # CloudWatch alarms, dashboards
├── environments/
│   ├── dev/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   ├── outputs.tf
│   │   ├── terraform.tfvars   # gitignored, per-env values
│   │   └── backend.tf         # S3 backend config
│   ├── staging/
│   └── production/
└── shared/
    └── backend.tf             # S3 + DynamoDB state locking
```

### 3.2 Example Module: RDS PostgreSQL

```hcl
# infra/terraform/modules/rds/main.tf
resource "aws_db_subnet_group" "this" {
  name       = "${var.name_prefix}-db-subnet-group"
  subnet_ids = var.subnet_ids

  tags = var.tags
}

resource "aws_security_group" "this" {
  name        = "${var.name_prefix}-rds-sg"
  description = "RDS PostgreSQL security group"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    security_groups = var.allowed_security_groups
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_db_instance" "this" {
  identifier                 = "${var.name_prefix}-postgres"
  engine                     = "postgres"
  engine_version             = "16"
  instance_class             = var.instance_class
  allocated_storage          = var.allocated_storage
  storage_encrypted          = true
  kms_key_id                 = var.kms_key_id
  multi_az                   = true
  db_subnet_group_name       = aws_db_subnet_group.this.name
  vpc_security_group_ids     = [aws_security_group.this.id]
  backup_retention_period    = 35
  backup_window              = "03:00-04:00"
  maintenance_window         = "sun:04:00-sun:05:00"
  enhanced_monitoring_role_arn = aws_iam_role.enhanced_monitoring.arn
  monitoring_interval        = 60
  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]
  auto_minor_version_upgrade = true
  apply_immediately          = false
  deletion_protection        = var.environment == "production" ? true : false
  skip_final_snapshot        = false
  final_snapshot_identifier  = "${var.name_prefix}-final-snapshot"
  
  parameter_group_name = aws_db_parameter_group.this.name

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-postgres"
  })
}

resource "aws_db_parameter_group" "this" {
  name   = "${var.name_prefix}-pg-params"
  family = "postgres16"

  parameter {
    name  = "log_connections"
    value = "1"
  }

  parameter {
    name  = "log_disconnections"
    value = "1"
  }

  parameter {
    name  = "log_min_duration_statement"
    value = "1000"  # Log queries slower than 1s
  }

  parameter {
    name  = "shared_preload_libraries"
    value = "pg_stat_statements"
  }
}

resource "aws_db_instance" "read_replica" {
  count                  = var.environment == "production" ? 1 : 0
  identifier             = "${var.name_prefix}-postgres-replica"
  replicate_source_db    = aws_db_instance.this.id
  instance_class         = var.instance_class
  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [aws_security_group.this.id]
  storage_encrypted      = true
  kms_key_id             = var.kms_key_id
}

resource "aws_secretsmanager_secret" "db_credentials" {
  name        = "${var.name_prefix}/database/master"
  description = "Master credentials for RDS PostgreSQL"
  kms_key_id  = var.kms_key_id
}

resource "aws_secretsmanager_secret_version" "db_credentials" {
  secret_id = aws_secretsmanager_secret.db_credentials.id
  secret_string = jsonencode({
    username = "nms_admin"
    password = random_password.db_password.result
    engine   = "postgres"
    host     = aws_db_instance.this.address
    port     = 5432
    dbname   = "nms"
  })
}

resource "random_password" "db_password" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}
```

### 3.3 Environment Promotion Flow

```
dev (auto-apply on push to develop branch)
  ↓ manual approval
staging (auto-apply on merge to main branch, after approval)
  ↓ manual approval + tech lead sign-off
production (manual apply, tech lead sign-off required)
```

Terraform plan runs in CI on every PR. Apply requires manual approval for staging + production.

### 3.4 State Management

- State stored in S3 backend with DynamoDB locking.
- State file per environment (`dev.tfstate`, `staging.tfstate`, `production.tfstate`).
- State contains secrets — S3 bucket encrypted, access restricted to CI/CD IAM role.
- State never committed to git.

```hcl
# infra/terraform/environments/production/backend.tf
terraform {
  backend "s3" {
    bucket         = "nms-terraform-state-prod"
    key            = "production/terraform.tfstate"
    region         = "me-central-1"
    encrypt        = true
    dynamodb_table = "nms-terraform-locks"
  }
}
```

---

## 4. Helm Charts

### 4.1 NMS API Chart

```
infra/helm/nms-api/
├── Chart.yaml
├── values.yaml              # Default values
├── values-dev.yaml          # Dev overrides
├── values-staging.yaml
├── values-production.yaml
└── templates/
    ├── deployment.yaml
    ├── service.yaml
    ├── ingress.yaml
    ├── hpa.yaml
    ├── pdb.yaml
    ├── networkpolicy.yaml
    ├── serviceaccount.yaml
    ├── configmap.yaml
    ├── secret.yaml          # References AWS Secrets Manager via IRSA
    └── _helpers.tpl
```

### 4.2 Example: deployment.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "nms-api.fullname" . }}
  labels:
    {{- include "nms-api.labels" . | nindent 4 }}
spec:
  replicas: {{ .Values.replicaCount }}
  selector:
    matchLabels:
      {{- include "nms-api.selectorLabels" . | nindent 6 }}
  template:
    metadata:
      annotations:
        checksum/config: {{ include (print $.Template.BasePath "/configmap.yaml") . | sha256sum }}
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
      labels:
        {{- include "nms-api.selectorLabels" . | nindent 8 }}
    spec:
      serviceAccountName: {{ include "nms-api.serviceAccountName" . }}
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        fsGroup: 1000
      containers:
      - name: api
        image: "{{ .Values.image.repository }}:{{ .Values.image.tag | default .Chart.AppVersion }}"
        imagePullPolicy: IfNotPresent
        ports:
        - name: http
          containerPort: 8080
          protocol: TCP
        envFrom:
        - configMapRef:
            name: {{ include "nms-api.fullname" . }}-config
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: {{ .Values.environment | quote }}
        - name: DATABASE__CONNECTIONSTRING
          valueFrom:
            secretKeyRef:
              name: {{ include "nms-api.fullname" . }}-secrets
              key: database-connection
        # ... more secrets
        resources:
          {{- toYaml .Values.resources | nindent 10 }}
        livenessProbe:
          httpGet:
            path: /health/live
            port: http
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
        readinessProbe:
          httpGet:
            path: /health/ready
            port: http
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
        startupProbe:
          httpGet:
            path: /health/startup
            port: http
          failureThreshold: 30
          periodSeconds: 10
        volumeMounts:
        - name: tmp
          mountPath: /tmp
      volumes:
      - name: tmp
        emptyDir: {}
      affinity:
        podAntiAffinity:
          preferredDuringSchedulingIgnoredDuringExecution:
          - weight: 100
            podAffinityTerm:
              labelSelector:
                matchExpressions:
                - key: app.kubernetes.io/name
                  operator: In
                  values:
                  - {{ include "nms-api.name" . }}
              topologyKey: kubernetes.io/hostname
      tolerations:
      - key: "node.kubernetes.io/not-ready"
        operator: "Exists"
        effect: "NoExecute"
        tolerationSeconds: 30
```

### 4.3 Secrets via AWS Secrets Manager (IRSA)

Avoid Kubernetes secrets (limited rotation, audit gaps). Instead, use AWS Secrets Manager + IAM Roles for Service Accounts (IRSA):

```yaml
# serviceaccount.yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: {{ include "nms-api.serviceAccountName" . }}
  annotations:
    eks.amazonaws.com/role-arn: {{ .Values.iam.roleArn | quote }}
```

The .NET app uses AWS SDK to fetch secrets at startup:

```csharp
// Program.cs
builder.Services.AddSingleton<ISecretsProvider>(sp =>
{
    return new AwsSecretsManagerProvider(
        region: builder.Configuration["AWS:Region"],
        fallbackToEnv: builder.Environment.IsDevelopment()
    );
});

// Custom configuration provider
builder.Configuration.AddSecretsManager(config =>
{
    config.SecretArn = "nms/prod/database";
    config.Key = "Database";
});
```

---

## 5. CI/CD Pipeline

### 5.1 Pipeline Stages

```
PR Check → Build → Test → Security Scan → Docker Build → Push to Registry
  ↓ (merge to develop)
Deploy to Dev → Smoke Tests
  ↓ (merge to main)
Deploy to Staging → E2E Tests → Performance Benchmarks
  ↓ (manual approval + tech lead sign-off)
Deploy to Production (Blue/Green) → Health Checks → Live
```

### 5.2 GitHub Actions Workflow

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [develop, main]
  pull_request:
    branches: [develop, main]

env:
  DOTNET_VERSION: '8.0.x'
  NODE_VERSION: '20.x'

jobs:
  # ---------- BACKEND ----------
  backend-test:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_PASSWORD: test
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports: ['5432:5432']
      redis:
        image: redis:7
        ports: ['6379:6379']
      rabbitmq:
        image: rabbitmq:3.13-management
        ports: ['5672:5672']
    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    - run: dotnet restore backend/NMS.sln
    - run: dotnet build backend/NMS.sln --configuration Release --no-restore
    - run: dotnet test backend/NMS.sln --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage" --logger "trx;LogFileName=test-results.trx"
    - uses: codecov/codecov-action@v4
      with:
        files: ./backend/**/coverage.cobertura.xml
        flags: backend
    - uses: SonarSource/sonarcloud-github-action@master
      env:
        SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
      with:
        projectBaseDir: backend
    - name: OWASP Dependency Check
      uses: dependency-check/Dependency-Check_Action@main
      with:
        project: 'NMS Backend'
        path: 'backend'
        format: 'HTML'

  # ---------- FRONTEND (Web) ----------
  web-test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: pnpm/action-setup@v4
      with:
        version: 9
    - uses: actions/setup-node@v4
      with:
        node-version: ${{ env.NODE_VERSION }}
        cache: 'pnpm'
        cache-dependency-path: web/pnpm-lock.yaml
    - run: pnpm install --frozen-lockfile
      working-directory: web
    - run: pnpm lint
      working-directory: web
    - run: pnpm typecheck
      working-directory: web
    - run: pnpm test:unit -- --coverage
      working-directory: web
    - uses: codecov/codecov-action@v4
      with:
        files: ./web/coverage/lcov.info
        flags: web

  # ---------- MOBILE ----------
  mobile-test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: pnpm/action-setup@v4
      with:
        version: 9
    - uses: actions/setup-node@v4
      with:
        node-version: ${{ env.NODE_VERSION }}
        cache: 'pnpm'
        cache-dependency-path: mobile/pnpm-lock.yaml
    - run: pnpm install --frozen-lockfile
      working-directory: mobile
    - run: pnpm lint
      working-directory: mobile
    - run: pnpm typecheck
      working-directory: mobile
    - run: pnpm test:unit
      working-directory: mobile

  # ---------- DOCKER BUILD ----------
  docker-build:
    needs: [backend-test, web-test, mobile-test]
    runs-on: ubuntu-latest
    if: github.event_name == 'push'
    strategy:
      matrix:
        target: [api, web]
    steps:
    - uses: actions/checkout@v4
    - uses: docker/setup-buildx-action@v3
    - uses: docker/login-action@v3
      with:
        registry: ghcr.io
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}
    - uses: docker/build-push-action@v5
      with:
        context: .
        file: infra/docker/${{ matrix.target }}.Dockerfile
        push: true
        tags: |
          ghcr.io/${{ github.repository }}/nms-${{ matrix.target }}:latest
          ghcr.io/${{ github.repository }}/nms-${{ matrix.target }}:${{ github.sha }}
        cache-from: type=gha
        cache-to: type=gha,mode=max
    - uses: aquasecurity/trivy-action@master
      with:
        image-ref: ghcr.io/${{ github.repository }}/nms-${{ matrix.target }}:${{ github.sha }}
        format: 'sarif'
        output: 'trivy-results.sarif'
    - uses: github/codeql-action/upload-sarif@v3
      with:
        sarif_file: 'trivy-results.sarif'

  # ---------- DEPLOY TO DEV ----------
  deploy-dev:
    needs: [docker-build]
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/develop'
    environment: dev
    steps:
    - uses: actions/checkout@v4
    - uses: azure/setup-kubectl@v4
    - uses: azure/setup-helm@v4
    - run: |
        aws eks update-kubeconfig --name nms-dev --region me-central-1
    - run: |
        helm upgrade --install nms-api infra/helm/nms-api \
          --namespace dev \
          --values infra/helm/nms-api/values-dev.yaml \
          --set image.tag=${{ github.sha }} \
          --wait --timeout 5m
    - run: |
        helm upgrade --install nms-web infra/helm/nms-web \
          --namespace dev \
          --values infra/helm/nms-web/values-dev.yaml \
          --set image.tag=${{ github.sha }} \
          --wait --timeout 5m
    - name: Smoke tests
      run: |
        kubectl -n dev rollout status deployment/nms-api --timeout=5m
        kubectl -n dev rollout status deployment/nms-web --timeout=5m
        curl -f https://dev-api.nms.ae/health/ready

  # ---------- DEPLOY TO STAGING ----------
  deploy-staging:
    needs: [docker-build]
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment: staging
    steps:
    - uses: actions/checkout@v4
    - uses: azure/setup-kubectl@v4
    - uses: azure/setup-helm@v4
    - run: aws eks update-kubeconfig --name nms-staging --region me-central-1
    - run: |
        helm upgrade --install nms-api infra/helm/nms-api \
          --namespace staging \
          --values infra/helm/nms-api/values-staging.yaml \
          --set image.tag=${{ github.sha }} \
          --wait --timeout 10m
    - run: |
        helm upgrade --install nms-web infra/helm/nms-web \
          --namespace staging \
          --values infra/helm/nms-web/values-staging.yaml \
          --set image.tag=${{ github.sha }} \
          --wait --timeout 10m
    - name: Run E2E tests (Playwright)
      run: |
        cd web && pnpm test:e2e -- --base-url=https://staging.nms.ae
    - name: Performance benchmark (k6)
      run: |
        k6 run infra/load-tests/dashboard-load.js --env BASE_URL=https://staging.nms.ae

  # ---------- DEPLOY TO PRODUCTION ----------
  deploy-production:
    needs: [deploy-staging]
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment: production  # Requires manual approval in GitHub Environment settings
    steps:
    - uses: actions/checkout@v4
    - uses: azure/setup-kubectl@v4
    - uses: azure/setup-helm@v4
    - run: aws eks update-kubeconfig --name nms-production --region me-central-1
    - name: Pre-deploy DB backup
      run: |
        aws rds create-db-snapshot \
          --db-instance-identifier nms-production-postgres \
          --db-snapshot-identifier nms-pre-deploy-$(date +%Y%m%d%H%M%S)
    - name: Blue/Green deploy
      run: |
        # Deploy to green (new) slots
        helm upgrade --install nms-api-green infra/helm/nms-api \
          --namespace production \
          --values infra/helm/nms-api/values-production.yaml \
          --set image.tag=${{ github.sha }} \
          --set deployment.slot=green \
          --wait --timeout 15m
        # Run health checks on green
        kubectl -n production exec deploy/nms-api-green -- curl -f http://localhost:8080/health/ready
        # Switch service selector to green
        kubectl -n production patch service nms-api -p '{"spec":{"selector":{"slot":"green"}}}'
        # Wait 5 minutes, then keep or rollback
        sleep 300
        # Scale down blue (old) after successful green
        kubectl -n production scale deploy/nms-api-blue --replicas=0
    - name: Rollback on failure
      if: failure()
      run: |
        kubectl -n production patch service nms-api -p '{"spec":{"selector":{"slot":"blue"}}}'
        kubectl -n production scale deploy/nms-api-green --replicas=0
```

### 5.3 Branch Protection Rules

- `main`: requires PR review (2 approvals), status checks pass, branches up to date.
- `develop`: requires PR review (1 approval), status checks pass.
- No force pushes to main or develop.
- No direct commits to main or develop (must come via PR).

### 5.4 Environment Protection Rules (GitHub Environments)

- `dev`: No approval required. Auto-deploy on merge to develop.
- `staging`: Auto-deploy on merge to main, but required reviewers (Tech Lead + 1) for any manual deploy.
- `production`: Required reviewers (Tech Lead + Ops Lead). Manual approval required before deploy.

---

## 6. Environment Strategy

### 6.1 Local Development

```
docker compose up  # From infra/docker/docker-compose.yml
  → PostgreSQL on :5432
  → Redis on :6379
  → RabbitMQ on :5672 (mgmt :15672)
  → Keycloak on :8080
  → MinIO on :9000 (console :9001)
  → MailHog on :1025 (UI :8025)

dotnet watch run --project backend/src/NMS.Api  # Backend hot reload on :5000
pnpm dev  # In web/ — Vite dev server on :5173
pnpm start  # In mobile/ — Expo dev server on :8081
```

### 6.2 Dev Environment (Shared)

- K8s `dev` namespace.
- 1 replica per service (cost-optimized).
- Shared PostgreSQL dev database (separate schemas per developer optional, or shared schema with seed data).
- Seed data: 1 test tenant, 3 staff users (admin, teacher, receptionist), 5 students with guardians, 2 rooms, 2 programs.
- Auto-deployed on merge to `develop` branch.
- Reset weekly (Monday 3 AM).
- Accessible at `dev.nms.ae` (auth required).

### 6.3 Staging Environment

- Full production mirror (3 replicas, multi-AZ).
- Anonymized production data snapshot (refreshed monthly).
- All integrations active: FCM sandbox, SES sandbox, Twilio test mode.
- Auto-deployed on merge to `main` branch.
- UAT environment for stakeholders.
- Accessible at `staging.nms.ae`.

### 6.4 Production Environment

- Full HA: 3+ replicas, multi-AZ, auto-scaling.
- All monitoring + alerting active.
- Manual approval gate in GitHub Actions.
- Tech Lead sign-off required.
- Blue/green deployment for zero-downtime.
- Accessible at `app.nms.ae` (web), `api.nms.ae` (API).

### 6.5 Environment Promotion Matrix

| Stage | Source | Trigger | Approval required |
|---|---|---|---|
| Local → Dev | Push to `develop` branch | Automatic | No |
| Dev → Staging | Merge `develop` to `main` | Automatic | No |
| Staging → Production | Manual trigger from `main` | Manual | Yes (Tech Lead + Ops) |

---

## 7. Observability Stack

### 7.1 Metrics (Prometheus + Grafana)

**Prometheus** scrapes metrics from:
- NMS API (`/metrics` endpoint via `prometheus-net.AspNetCore`): HTTP rate, latency histogram, error rate, EF Core pool, RabbitMQ publisher/consumer, Hangfire jobs.
- NMS Web (via `nginx-prometheus-exporter` sidecar): request rate, response time.
- PostgreSQL (`postgres_exporter`): connections, cache hit rate, slow queries.
- Redis (`redis_exporter`): memory, hit rate, ops/sec.
- RabbitMQ (built-in Prometheus endpoint): queue depth, publish rate, consume rate.
- K8s (`kube-state-metrics` + `node_exporter`): pod count, node CPU/memory, pod restarts.
- Keycloak (built-in metrics): active sessions, logins/sec, errors.

**Grafana dashboards** (committed in `infra/monitoring/dashboards/`):
- `nms-overview.json` — Top-level system health (uptime, error rate, request rate, p99 latency).
- `nms-business-kpis.json` — Daily attendance rate, active users, overdue invoices, ratio breaches.
- `nms-api-performance.json` — Endpoint-by-endpoint latency heatmap, error rate by status code.
- `nms-infrastructure.json` — K8s cluster, pods, nodes, PVCs.
- `nms-database.json` — PostgreSQL connections, slow queries, cache hit rate.
- `nms-notifications.json` — Notification delivery rate by channel, retry rate, DLQ depth.

**Alert rules** (in `infra/monitoring/alerts/`):
- HTTP 5xx rate > 1% for 5 min → PagerDuty (critical).
- p99 latency > 3s for 5 min → PagerDuty (critical).
- DB pool > 80% for 5 min → Slack #ops-alerts (warning).
- RabbitMQ queue depth > 500 for 5 min → Slack #ops-alerts (warning).
- K8s pod restart loop (5 restarts in 10 min) → PagerDuty (critical).
- Backup failed → PagerDuty (critical).
- Certificate expiring in 7 days → Slack #ops-alerts (warning).
- Disk usage > 80% → Slack #ops-alerts (warning).

### 7.2 Logs (Loki + Grafana)

- Backend: Serilog with JSON sink → Promtail → Loki → Grafana.
- Frontend: Sentry (errors + performance monitoring).
- Mobile: Sentry (crashes + performance).
- K8s: Loki collects all pod logs via Promtail daemonset.
- Log retention: 90 days hot (Loki), 365 days cold (S3).

**Structured logging** in .NET:
```csharp
_logger.LogInformation("Student checked in. StudentId: {StudentId}, RoomId: {RoomId}, Method: {Method}", 
    studentId, roomId, method);
```

All logs include: `timestamp`, `level`, `message_template`, `request_id`, `tenant_id`, `user_id`, `entity_type`, `entity_id` (where applicable).

### 7.3 Tracing (OpenTelemetry + Tempo)

- OpenTelemetry .NET SDK instruments: ASP.NET Core, EF Core, HttpClient, StackExchange.Redis, RabbitMQ.
- Traces exported to OpenTelemetry Collector → Tempo.
- Cross-service tracing critical when Notification + Media Processing services extract in Phase 3.
- Sampling: 100% for errors, 10% for normal requests in production (100% in dev/staging).

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddStackExchangeRedisInstrumentation()
        .AddOtlpExporter(opt => opt.Endpoint = new Uri(builder.Configuration["OpenTelemetry:Endpoint"])));
```

### 7.4 Synthetics

Grafana Synthetics (or AWS CloudWatch Synthetics) monitors:
- Login flow (every 5 min from UAE region).
- Dashboard load (every 5 min).
- Parent portal load (every 5 min).
- API health endpoint (every 1 min).
- SignalR connection (every 5 min).

Alert if any synthetic fails 3x in a row.

---

## 8. Backup & Recovery

### 8.1 Backup Strategy

| Data | Frequency | Method | Retention | RPO | RTO |
|---|---|---|---|---|---|
| PostgreSQL (SaaS) | Daily snapshot + WAL (5 min) | RDS automated | 35 days | 5 min | 1 hour |
| PostgreSQL (on-prem) | Daily + WAL | pgBackRest | Customer-defined | Customer-defined | Customer-defined |
| Object storage (S3) | Continuous cross-region replication | S3 CRR | Lifecycle (move to Glacier after 90d, delete after 7y) | 0 | 15 min |
| Object storage (MinIO) | Daily replication | MinIO bucket replication | Customer-defined | Customer-defined | Customer-defined |
| Redis | Not backed up (cache only) | N/A | N/A | N/A | N/A |
| RabbitMQ | Not backed up (transient) | N/A | N/A | N/A | N/A |
| Keycloak DB | Daily snapshot + WAL | Same as PostgreSQL | 35 days | 5 min | 1 hour |
| Terraform state | Continuous (git + S3) | Git + S3 versioning | Indefinite | 0 | 15 min |
| Container images | ECR lifecycle policy | ECR | Last 20 per repo | 0 | 15 min |

### 8.2 Recovery Procedures

`infra/docs/disaster-recovery.md` documents:

**PostgreSQL point-in-time recovery:**
1. Identify recovery timestamp.
2. `aws rds restore-db-instance-to-point-in-time --source-db-instance-identifier nms-production-postgres --target-db-instance-identifier nms-restored --restore-time 2026-05-17T10:00:00Z`
3. Wait for restore (15-60 min depending on size).
4. Update connection string to restored instance.
5. Restart API pods.
6. Verify data integrity.
7. Switch DNS to restored instance.

**Object storage recovery:**
- S3 versioning enables per-object restore (previous version).
- Bulk restore: S3 Batch Operations job.

**Keycloak realm recovery:**
- Keycloak supports realm export/import.
- Export realm JSON, import to fresh Keycloak instance.

### 8.3 DR Drills

Quarterly DR drill:
1. Pick a date (off-hours, weekend).
2. Notify stakeholders.
3. Restore full stack to DR environment (AWS eu-central-1 if multi-region, or staging if single-region).
4. Run smoke tests against DR environment.
5. Verify critical flows (login, check-in, post activity, send invoice).
6. Document time-to-restore.
7. Tear down DR environment.
8. Post-mortem: what worked, what didn't, action items.

---

## 9. Security Hardening

### 9.1 Cluster Security

- EKS cluster endpoint: private (no public access).
- Worker nodes: private subnets only.
- IAM roles for service accounts (IRSA) — no long-lived AWS credentials in pods.
- Secrets Manager for all secrets — no Kubernetes Secrets (limited rotation, audit gaps).
- Network policies: default deny, allow explicitly.
- Pod Security Standards: `restricted` profile enforced via PodSecurityPolicy / Kyverno.
- Image scanning: Trivy on every image push, fail build on CRITICAL vulnerabilities.
- Image registry: private (ghcr.io or ECR), no public images in production.
- Image immutability: production tags are immutable (registry config).

### 9.2 Network Security

- WAF on ALB (OWASP Top 10 rules + custom rules for known attack patterns).
- CloudFront in front of S3 (signed URLs only).
- DDoS protection: AWS Shield Standard (free, automatic).
- TLS 1.2+ everywhere (TLS 1.3 where supported).
- mTLS between pods (via Istio service mesh in Phase 2+).
- Rate limiting at ALB + application level.

### 9.3 Compliance Scanning

- `kube-bench` weekly (CIS Kubernetes Benchmark).
- `kube-hunter` weekly (known vulnerabilities).
- `tfsec` on every Terraform PR.
- `checkov` on every Terraform PR.
- AWS Security Hub: enabled, findings triaged weekly.
- AWS GuardDuty: enabled, alerts to PagerDuty.

---

## 10. Release Management

### 10.1 Versioning

- **Semantic versioning**: `MAJOR.MINOR.PATCH` (e.g., `1.4.2`).
- MAJOR: breaking changes (rare, with v2 API version).
- MINOR: new features (backward compatible).
- PATCH: bug fixes.
- Pre-release: `1.0.0-rc.1`, `1.0.0-beta.1`.

### 10.2 Release Process

1. **Release branch**: `release/1.4.0` cut from `main`.
2. **Release candidate**: build, deploy to staging, run full E2E + performance tests.
3. **Bug fixes**: cherry-pick to release branch.
4. **Release**: tag `v1.4.0`, build Docker images with tag, deploy to production.
5. **Post-release**: merge release branch back to `main` + `develop`.
6. **Release notes**: generated from PR titles, published to `docs/releases/` + emailed to customers.

### 10.3 LTS Branches

For on-prem customers who don't want bleeding edge:
- Maintain 2 LTS versions (e.g., `1.x` and `2.x`).
- LTS branches receive security + critical bug fixes only (no new features).
- LTS support: 18 months after next major release.

### 10.4 Rollback

- Blue/green deployment: switch service selector back to blue (old) slot.
- Database migrations: each has `Down` method. Tested in staging.
- Helm rollback: `helm rollback nms-api <previous-revision>` (manual).
- Automatic rollback: if health checks fail within 5 min post-deploy, Helm rolls back automatically.

---

## 11. Local Dev Setup (Detailed)

### 11.1 Prerequisites

```bash
# Install required tools
brew install dotnet node pnpm git docker kubectl helm terraform awscli
# OR on Linux:
# apt install dotnet-sdk-8.0 nodejs npm git docker.io kubectl terraform awscli
# install pnpm: npm install -g pnpm
# install helm: https://helm.sh/docs/intro/install/

# Install Expo CLI
pnpm add -g expo-cli

# Verify versions
dotnet --version  # 8.0.x
node --version    # v20.x
pnpm --version    # 9.x
docker --version  # 24.x
kubectl version --client
helm version
terraform version  # 1.9+
```

### 11.2 First-Time Setup

```bash
# Clone repo
git clone https://github.com/<org>/nms.git
cd nms

# Start local infrastructure
cd infra/docker
docker compose up -d
# Wait for services to be healthy (check: docker compose ps)

# Initialize Keycloak
# Open http://localhost:8080, login admin/admin
# Import realm from infra/keycloak/nms-realm.json

# Backend
cd ../../backend
dotnet restore
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/NMS.Infrastructure --startup-project src/NMS.Api
dotnet seed-data  # Custom command to seed dev data
dotnet watch run --project src/NMS.Api
# API available at http://localhost:5000

# Web
cd ../web
pnpm install
cp .env.example .env  # Configure API URL
pnpm dev
# Web available at http://localhost:5173

# Mobile (optional)
cd ../mobile
pnpm install
cp .env.example .env
pnpm start
# Scan QR code with Expo Go app
```

### 11.3 Useful Makefile Targets

```makefile
# Makefile
.PHONY: help setup dev test clean

help:
	@echo "Available targets:"
	@echo "  setup    - First-time setup"
	@echo "  dev      - Start local dev (backend + web + infra)"
	@echo "  test     - Run all tests"
	@echo "  clean    - Clean all build artifacts"

setup:
	cd infra/docker && docker compose up -d
	dotnet tool install --global dotnet-ef
	cd backend && dotnet restore
	cd web && pnpm install
	cd mobile && pnpm install

dev:
	# Terminal 1: infrastructure (already running from setup)
	# Terminal 2: backend
	cd backend && dotnet watch run --project src/NMS.Api
	# Terminal 3: web
	cd web && pnpm dev

test:
	cd backend && dotnet test
	cd web && pnpm test
	cd mobile && pnpm test

clean:
	find . -type d -name bin -prune -exec rm -rf {} \;
	find . -type d -name obj -prune -exec rm -rf {} \;
	find . -type d -name node_modules -prune -exec rm -rf {} \;
	rm -rf web/dist web/coverage
	rm -rf mobile/.expo
```

---

## 12. Runbooks

`infra/docs/runbooks/` contains operational runbooks for common scenarios:

- `incident-response.md` — How to handle SEV-1/SEV-2 incidents.
- `disaster-recovery.md` — Full DR procedures.
- `database-maintenance.md` — Vacuum, index rebuild, partition management.
- `keycloak-realm-management.md` — Creating/migrating realms, user federation setup.
- `tenant-provisioning.md` — Manual tenant provisioning (when self-service fails).
- `tenant-suspension.md` — Suspending a tenant (non-payment, abuse).
- `backup-restore.md` — Restoring from backups (per data type).
- `certificate-renewal.md` — Manual TLS cert renewal (if cert-manager fails).
- `secrets-rotation.md` — Rotating secrets (DB password, API keys).
- `k8s-node-replacement.md` — Replacing a failed K8s node.
- `performance-debugging.md` — Diagnosing slow API / slow dashboard.

Each runbook includes: when to use, prerequisites, step-by-step, verification, rollback, escalation.

---

**Next file to read**: `12-WEB-FRONTEND.md` for React architecture, routing, i18n, and component patterns.
