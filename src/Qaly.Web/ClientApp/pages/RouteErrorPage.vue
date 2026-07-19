<template>
  <main class="route-error-page" role="alert" aria-labelledby="route-error-title">
    <section class="route-error-panel">
      <span class="route-error-code">Error</span>
      <h1 id="route-error-title">{{ title }}</h1>
      <p>{{ message }}</p>
      <RouterLink class="route-error-action" to="/dashboard">Về tổng quan</RouterLink>
    </section>
  </main>
</template>

<script setup lang="ts">
defineProps<{
  title: string
  message: string
}>()
</script>

<style scoped>
.route-error-page {
  min-height: calc(100vh - 72px);
  display: grid;
  place-items: center;
  padding: 32px 18px;
  background: var(--bg, #f8fafc);
}

.route-error-panel {
  width: min(560px, 100%);
  border: 1px solid var(--line, #e2e8f0);
  border-radius: 8px;
  padding: 28px;
  background: var(--panel, #ffffff);
  box-shadow: var(--qaly-shadow-sm, 0 12px 30px rgba(15, 23, 42, 0.08));
}

.route-error-code {
  display: inline-flex;
  margin-bottom: 12px;
  color: #b91c1c;
  font-size: 0.75rem;
  font-weight: 800;
  text-transform: uppercase;
}

.route-error-panel h1 {
  margin: 0;
  color: var(--text-strong, #0f172a);
  font-size: 1.4rem;
  line-height: 1.2;
}

.route-error-panel p {
  margin: 10px 0 22px;
  color: var(--text-muted, #64748b);
  line-height: 1.6;
}

.route-error-action {
  display: inline-flex;
  align-items: center;
  min-height: 38px;
  padding: 0 14px;
  border-radius: 6px;
  background: var(--primary, #2563eb);
  color: #ffffff;
  font-weight: 700;
  text-decoration: none;
}
</style>
