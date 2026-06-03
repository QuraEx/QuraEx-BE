#!/usr/bin/env bash
# One-time provisioning for the QuraEx production droplet (Ubuntu 24.04).
# Installs Docker Engine + compose plugin, creates the deploy directory layout,
# and locks the host firewall down to SSH only (Cloudflare Tunnel is outbound-only,
# so no inbound web ports are needed).
#
# Run once as root (or with sudo) on the droplet:
#   curl -fsSL https://raw.githubusercontent.com/bavanchun/QuraEx-BE/main/quraexv2/deploy/setup-droplet.sh | sudo bash
# or copy this file over and: sudo bash setup-droplet.sh
set -euo pipefail

DEPLOY_DIR=/opt/quraex

echo "==> Installing Docker Engine + compose plugin"
if ! command -v docker >/dev/null 2>&1; then
  apt-get update
  apt-get install -y ca-certificates curl gnupg
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
    | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
  chmod a+r /etc/apt/keyrings/docker.gpg
  echo \
    "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    > /etc/apt/sources.list.d/docker.list
  apt-get update
  apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  systemctl enable --now docker
else
  echo "    Docker already installed: $(docker --version)"
fi

echo "==> Creating deploy layout at ${DEPLOY_DIR}"
mkdir -p "${DEPLOY_DIR}/config"
echo "    ${DEPLOY_DIR}/                  (docker-compose.prod.yml lands here via CI)"
echo "    ${DEPLOY_DIR}/.env              (create from deploy/.env.prod.example)"
echo "    ${DEPLOY_DIR}/config/*.json     (create from deploy/config/*.example)"

echo "==> Host firewall (UFW): allow SSH only"
if command -v ufw >/dev/null 2>&1; then
  ufw allow OpenSSH
  ufw --force enable
  ufw status verbose
else
  echo "    ufw not present — skipping (rely on DigitalOcean Cloud Firewall: allow 22 inbound only)."
fi

echo "==> Done. Next: create ${DEPLOY_DIR}/.env and ${DEPLOY_DIR}/config/*.json, then push to main to trigger the first deploy."
