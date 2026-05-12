<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Key, Trash2, Copy, Check, ShieldAlert } from 'lucide-vue-next'

const apiKeys = ref<any[]>([])
const newKeyName = ref('')
const generatedKey = ref<string | null>(null)
const isLoading = ref(false)
const copied = ref(false)

async function fetchKeys() {
  const res = await fetch('/api/auth/api-keys')
  if (res.ok) {
    const data = await res.json()
    apiKeys.value = data.data
  }
}

async function createKey() {
  if (!newKeyName.value) return
  isLoading.value = true
  const res = await fetch('/api/auth/api-keys', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name: newKeyName.value })
  })
  if (res.ok) {
    const data = await res.json()
    generatedKey.value = data.data.key
    newKeyName.value = ''
    await fetchKeys()
  }
  isLoading.value = false
}

async function revokeKey(id: string) {
  if (!confirm('Bạn có chắc chắn muốn thu hồi API Key này?')) return
  const res = await fetch(`/api/auth/api-keys/${id}`, { method: 'DELETE' })
  if (res.ok) await fetchKeys()
}

function copyToClipboard() {
  if (!generatedKey.value) return
  navigator.clipboard.writeText(generatedKey.value)
  copied.value = true
  setTimeout(() => copied.value = false, 2000)
}

onMounted(fetchKeys)
</script>

<template>
  <div class="api-keys-manager glass-card reveal">
    <div class="panel-header">
      <Key :size="20" class="icon-primary" />
      <h3>Quản lý API Keys</h3>
    </div>
    
    <p class="panel-desc">Sử dụng API Keys để tích hợp Qaly với các ứng dụng bên ngoài của bạn.</p>

    <div v-if="generatedKey" class="key-display-banner glass-card">
      <div class="banner-icon"><ShieldAlert :size="24" /></div>
      <div class="banner-content">
        <strong>Lưu lại API Key của bạn!</strong>
        <p>Vì lý do bảo mật, chúng tôi chỉ hiển thị key này một lần duy nhất. Bạn sẽ không thể xem lại nó sau khi đóng thông báo này.</p>
        <div class="key-box">
          <code>{{ generatedKey }}</code>
          <button @click="copyToClipboard" class="icon-button">
            <Check v-if="copied" :size="16" color="var(--success)" />
            <Copy v-else :size="16" />
          </button>
        </div>
        <button class="primary-button primary-button--compact" @click="generatedKey = null">Tôi đã lưu xong</button>
      </div>
    </div>

    <form @submit.prevent="createKey" class="create-key-form">
      <input v-model="newKeyName" type="text" placeholder="Tên gợi nhớ (vd: CI/CD Pipeline)" required />
      <button type="submit" class="primary-button" :disabled="isLoading">
        {{ isLoading ? 'Đang tạo...' : 'Tạo Key mới' }}
      </button>
    </form>

    <div class="keys-list">
      <div v-for="key in apiKeys" :key="key.id" class="key-item glass-card">
        <div class="key-info">
          <strong>{{ key.name }}</strong>
          <span class="key-prefix">Prefix: <code>{{ key.keyPrefix }}...</code></span>
          <span class="key-date">Tạo ngày: {{ new Date(key.createdAt).toLocaleDateString() }}</span>
        </div>
        <button @click="revokeKey(key.id)" class="revoke-button" title="Thu hồi">
          <Trash2 :size="16" />
        </button>
      </div>
      <div v-if="apiKeys.length === 0" class="empty-state">Chưa có API Key nào.</div>
    </div>
  </div>
</template>

<style scoped>
.api-keys-manager { padding: 24px; }
.panel-header { display: flex; align-items: center; gap: 12px; margin-bottom: 8px; }
.panel-desc { color: var(--muted); font-size: 14px; margin-bottom: 24px; }

.create-key-form { display: flex; gap: 12px; margin-bottom: 24px; }
.create-key-form input { flex: 1; border: 1px solid var(--line); border-radius: 8px; padding: 10px 16px; outline: none; }

.key-display-banner { 
  display: flex; gap: 16px; padding: 20px; border-left: 4px solid var(--warning);
  background: var(--warning-soft); margin-bottom: 24px;
}
.key-box { 
  display: flex; align-items: center; gap: 8px; background: white; 
  padding: 8px 12px; border-radius: 6px; margin: 12px 0; border: 1px solid var(--warning);
}
.key-box code { font-family: monospace; font-weight: 700; color: var(--warning-dark); flex: 1; word-break: break-all; }

.keys-list { display: flex; flex-direction: column; gap: 12px; }
.key-item { display: flex; justify-content: space-between; align-items: center; padding: 16px; }
.key-info { display: flex; flex-direction: column; gap: 4px; }
.key-prefix { font-size: 12px; color: var(--muted); }
.key-date { font-size: 11px; color: var(--muted); opacity: 0.8; }

.revoke-button { 
  border: none; background: transparent; color: var(--peach-500); 
  cursor: pointer; padding: 8px; border-radius: 6px; transition: background 0.2s;
}
.revoke-button:hover { background: var(--peach-100); }

.empty-state { text-align: center; color: var(--muted); padding: 20px; font-style: italic; }
</style>
