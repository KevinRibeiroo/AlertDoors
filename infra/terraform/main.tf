locals {
  secret_names = {
    DISCORD_WEBHOOK_URL = "alertdoors-discord-webhook"
    SMTP_HOST           = "alertdoors-smtp-host"
    SMTP_PORT           = "alertdoors-smtp-port"
    SMTP_USER           = "alertdoors-smtp-user"
    SMTP_PASSWORD       = "alertdoors-smtp-password"
    EMAIL_FROM          = "alertdoors-email-from"
    EMAIL_TO            = "alertdoors-email-to"
  }
  active_secrets = { for key, value in local.secret_names : key => value if(key == "DISCORD_WEBHOOK_URL" ? var.discord_enabled : var.email_enabled) }
}

resource "google_project_service" "apis" {
  for_each           = toset(["run.googleapis.com", "cloudscheduler.googleapis.com", "firestore.googleapis.com", "secretmanager.googleapis.com", "artifactregistry.googleapis.com", "iam.googleapis.com"])
  project            = var.project_id
  service            = each.value
  disable_on_destroy = false
}

resource "google_artifact_registry_repository" "images" {
  location      = var.region
  repository_id = "alertdoors"
  format        = "DOCKER"
  depends_on    = [google_project_service.apis]
}

resource "google_firestore_database" "state" {
  project                 = var.project_id
  name                    = "(default)"
  location_id             = var.region
  type                    = "FIRESTORE_NATIVE"
  database_edition        = "STANDARD"
  concurrency_mode        = "PESSIMISTIC"
  delete_protection_state = "DELETE_PROTECTION_ENABLED"
  lifecycle { prevent_destroy = true }
  depends_on = [google_project_service.apis]
}

resource "google_firestore_index" "pending" {
  project     = var.project_id
  database    = google_firestore_database.state.name
  collection  = "deliveries"
  query_scope = "COLLECTION"
  fields {
    field_path = "channel"
    order      = "ASCENDING"
  }
  fields {
    field_path = "status"
    order      = "ASCENDING"
  }
  fields {
    field_path = "createdAt"
    order      = "ASCENDING"
  }
}

resource "google_secret_manager_secret" "config" {
  for_each  = local.secret_names
  secret_id = each.value
  replication {
    auto {}
  }
  lifecycle { prevent_destroy = true }
  depends_on = [google_project_service.apis]
}

resource "google_cloud_run_v2_job" "bot" {
  count               = var.deploy_job ? 1 : 0
  name                = "alertdoors"
  location            = var.region
  deletion_protection = true
  template {
    task_count  = 1
    parallelism = 1
    template {
      service_account = google_service_account.runtime.email
      timeout         = "600s"
      max_retries     = 0
      containers {
        image = var.image_uri
        args  = var.job_args
        resources { limits = { cpu = "1", memory = "512Mi" } }
        env {
          name  = "GOOGLE_CLOUD_PROJECT"
          value = var.project_id
        }
        env {
          name  = "ALERTDOORS_MODE"
          value = "Production"
        }
        env {
          name  = "DISCORD_ENABLED"
          value = tostring(var.discord_enabled)
        }
        env {
          name  = "EMAIL_ENABLED"
          value = tostring(var.email_enabled)
        }
        dynamic "env" {
          for_each = local.active_secrets
          content {
            name = env.key
            value_source {
              secret_key_ref {
                secret  = google_secret_manager_secret.config[env.key].secret_id
                version = lookup(var.secret_versions, env.key, "1")
              }
            }
          }
        }
      }
    }
  }
  lifecycle {
    precondition {
      condition     = var.image_uri != ""
      error_message = "Publish the image and set image_uri before enabling deploy_job."
    }
    precondition {
      condition     = contains(var.job_args, "--dry-run") || var.discord_enabled || var.email_enabled
      error_message = "A real run requires at least one configured destination."
    }
  }
  depends_on = [google_project_service.apis, google_project_iam_member.state, google_secret_manager_secret_iam_member.runtime]
}
