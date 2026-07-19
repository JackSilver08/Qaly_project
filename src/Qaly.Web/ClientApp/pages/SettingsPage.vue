<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  User, Settings, Bell, Palette, Key, Database, Shield, Lock, Check,
  Activity, Cloud, Save, RefreshCw, Terminal, Globe, UserCheck, ShieldAlert,
  Sliders, Plus, CheckSquare, Image, Laugh
} from 'lucide-vue-next'
import { useDashboardContext } from '../composables/dashboard-context'
import ApiKeysTab from '../components/ApiKeysTab.vue'
import PrivacySettingsTab from '../components/settings/PrivacySettingsTab.vue'
import { showSuccess, showError } from '../composables/use-toast'

const { currentUser, displayRole, loadDashboard, projects, selectedProject } = useDashboardContext()

const activeTab = ref('profile')
const currentUserRole = computed(() => String(currentUser.value?.role || '').toLowerCase())
const isSystemAdmin = computed(() => currentUserRole.value === 'admin')
const selectedProjectMemberRole = computed(() => {
  const project = selectedProject.value
  const userId = currentUser.value?.id

  if (!project || !userId) return ''

  return (
    String(project.members?.find((member: any) => member.userId === userId)?.role || '')
      .trim()
      .toLowerCase()
  )
})
const canManageWorkspaceSettings = computed(() => isSystemAdmin.value)
const canManageProjectWorkflow = computed(() =>
  isSystemAdmin.value || ['owner', 'manager'].includes(selectedProjectMemberRole.value),
)

// Theme state
const themeStorageKey = 'qaly-theme'
const currentTheme = ref('light')
const accentColor = ref('sapphire')

// Settings values (mocked/stored in localStorage for state-of-the-art UX)
const name = ref('')
const email = ref('')
const selectedLang = ref('vi')
const sessionTimeout = ref(60)

// Organization and project list for configuration
const organizations = ref<any[]>([])
const selectedOrgId = ref<string | null>(null)
const selectedProjectId = ref<string | null>(null)

// Notion-style Workspace Customization
const allowedDomains = ref('qaly.dev, company.com')
const workspaceIcon = ref('🚀')
const workspaceCover = ref('deep-ocean')

// Jira-style Kanban Workflow settings
const enableOnHold = ref(true)
const enableInReview = ref(true)
const requireEvidenceToDone = ref(false)
const restrictTransitionsToAdmin = ref(false)

// Password Form
const passwordForm = ref({
  currentPassword: '',
  newPassword: '',
  confirmNewPassword: ''
})

// Notifications preferences
const notifEmailTask = ref(true)
const notifPushMention = ref(true)
const notifSoundMeeting = ref(true)
const notifWeeklyReport = ref(false)

// Save state
const isSaving = ref(false)
const isSavingPassword = ref(false)

// Audit logs
const auditLogs = ref<any[]>([])
const isLoadingLogs = ref(false)

async function loadOrganizations() {
  if (!canManageWorkspaceSettings.value) {
    organizations.value = []
    selectedOrgId.value = null
    return
  }

  try {
    const res = await fetch('/api/organizations')
    if (res.ok) {
      const payload = await res.json()
      organizations.value = payload.data?.items || []
      if (organizations.value.length > 0) {
        const match = selectedProject.value?.organizationId
          ? organizations.value.find((o: any) => o.id === selectedProject.value.organizationId)
          : null
        selectedOrgId.value = match ? match.id : organizations.value[0].id
        loadOrgSettings()
      }
    }
  } catch (e) {
    console.warn("Lỗi tải danh sách tổ chức:", e)
  }
}

function loadOrgSettings() {
  const org = organizations.value.find((o: any) => o.id === selectedOrgId.value)
  if (org) {
    allowedDomains.value = org.allowedEmailDomains || ''
    workspaceIcon.value = org.workspaceIcon || '🚀'
    workspaceCover.value = org.workspaceCover || 'deep-ocean'
  }
}

function loadProjectSettings() {
  if (!canManageProjectWorkflow.value) {
    selectedProjectId.value = null
    return
  }

  const proj = projects.value.find((p: any) => p.id === selectedProjectId.value)
  if (proj) {
    enableOnHold.value = proj.enableOnHold !== false
    enableInReview.value = proj.enableInReview !== false
    requireEvidenceToDone.value = proj.requireEvidenceToDone === true
    restrictTransitionsToAdmin.value = proj.restrictTransitionsToAdmin === true
  }
}

async function loadSettings() {
  currentTheme.value = localStorage.getItem(themeStorageKey) || 'light'
  accentColor.value = localStorage.getItem('qaly-accent') || 'sapphire'
  selectedLang.value = localStorage.getItem('qaly-lang') || 'vi'
  sessionTimeout.value = parseInt(localStorage.getItem('qaly-session-timeout') || '60')
  
  if (currentUser.value) {
    name.value = currentUser.value.fullName || ''
    email.value = currentUser.value.email || ''
  }
  
  notifEmailTask.value = localStorage.getItem('qaly-notif-email-task') !== 'false'
  notifPushMention.value = localStorage.getItem('qaly-notif-push-mention') !== 'false'
  notifSoundMeeting.value = localStorage.getItem('qaly-notif-sound-meeting') !== 'false'
  notifWeeklyReport.value = localStorage.getItem('qaly-notif-weekly-report') === 'true'
  
  if (projects.value.length > 0) {
    selectedProjectId.value = selectedProject.value?.id || projects.value[0].id
    loadProjectSettings()
  }
  
  await loadOrganizations()
}

function applyTheme(theme: 'light' | 'dark') {
  currentTheme.value = theme
  document.documentElement.dataset.theme = theme
  localStorage.setItem(themeStorageKey, theme)
  applyAccent(accentColor.value, theme)
}

function toggleTheme() {
  applyTheme(currentTheme.value === 'dark' ? 'light' : 'dark')
}

function selectAccent(color: string) {
  accentColor.value = color
  localStorage.setItem('qaly-accent', color)
  applyAccent(color, currentTheme.value as 'light' | 'dark')
  showSuccess(`Đã áp dụng tông màu chủ đạo ${color.toUpperCase()}`)
}

function applyAccent(color: string, theme: 'light' | 'dark') {
  const colors: Record<string, { light: string, dark: string, lightStrong: string, darkStrong: string }> = {
    sapphire: { light: '#0f52ba', dark: '#3b82f6', lightStrong: '#0a3d91', darkStrong: '#60a5fa' },
    emerald: { light: '#10b981', dark: '#34d399', lightStrong: '#065f46', darkStrong: '#6ee7b7' },
    amethyst: { light: '#8b5cf6', dark: '#a78bfa', lightStrong: '#5b21b6', darkStrong: '#c4b5fd' },
    rose: { light: '#f43f5e', dark: '#fb7185', lightStrong: '#9f1239', darkStrong: '#fca5a5' },
    amber: { light: '#f59e0b', dark: '#fbbf24', lightStrong: '#92400e', darkStrong: '#fde047' }
  }
  
  const selected = colors[color] || colors.sapphire
  const primaryColor = theme === 'light' ? selected.light : selected.dark
  const strongColor = theme === 'light' ? selected.lightStrong : selected.darkStrong
  
  document.documentElement.style.setProperty('--primary', primaryColor)
  document.documentElement.style.setProperty('--primary-strong', strongColor)
  document.documentElement.style.setProperty('--primary-hover', primaryColor)
  document.documentElement.style.setProperty('--primary-soft', theme === 'light' ? `${primaryColor}1a` : `${primaryColor}2d`)
}

async function fetchAuditLogs() {
  isLoadingLogs.value = true
  try {
    const res = await fetch('/api/audit-logs/mine?pageSize=15')
    if (res.ok) {
      const data = await res.json()
      auditLogs.value = data.data?.items || []
    }
  } catch (e) {
    console.warn("Lỗi tải nhật ký hoạt động:", e)
  } finally {
    isLoadingLogs.value = false
  }
}

async function changePassword() {
  if (passwordForm.value.newPassword !== passwordForm.value.confirmNewPassword) {
    showError("Mật khẩu mới xác nhận không khớp.")
    return
  }
  
  isSavingPassword.value = true
  try {
    const res = await fetch('/api/auth/change-password', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(passwordForm.value)
    })
    
    if (res.ok) {
      showSuccess("Đổi mật khẩu thành công! Vui lòng đăng nhập lại.")
      passwordForm.value = { currentPassword: '', newPassword: '', confirmNewPassword: '' }
      setTimeout(() => {
        window.location.href = "/Account/Login"
      }, 1500)
    } else {
      const err = await res.json()
      showError(err.error || "Mật khẩu hiện tại không chính xác.")
    }
  } catch (e) {
    showError("Đã xảy ra lỗi khi đổi mật khẩu.")
  } finally {
    isSavingPassword.value = false
  }
}

async function saveSettings() {
  isSaving.value = true
  let hadFailure = false
  
  // Persist local preferences
  localStorage.setItem('qaly-lang', selectedLang.value)
  localStorage.setItem('qaly-session-timeout', String(sessionTimeout.value))
  localStorage.setItem('qaly-notif-email-task', String(notifEmailTask.value))
  localStorage.setItem('qaly-notif-push-mention', String(notifPushMention.value))
  localStorage.setItem('qaly-notif-sound-meeting', String(notifSoundMeeting.value))
  localStorage.setItem('qaly-notif-weekly-report', String(notifWeeklyReport.value))
  
  // Call API to save name/profile changes if modified
  if (name.value.trim() && currentUser.value && name.value.trim() !== currentUser.value.fullName) {
    try {
      const res = await fetch('/api/auth/profile', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ fullName: name.value.trim(), avatarUrl: currentUser.value.avatarUrl })
      })
      if (res.ok) {
        currentUser.value.fullName = name.value.trim()
      } else {
        hadFailure = true
        showError('Không thể cập nhật hồ sơ người dùng.')
      }
    } catch (e) {
      hadFailure = true
      showError('Không thể cập nhật hồ sơ người dùng.')
      console.warn("Không thể cập nhật hồ sơ trên máy chủ:", e)
    }
  }

  // Update organization settings
  if (canManageWorkspaceSettings.value && selectedOrgId.value) {
    const org = organizations.value.find((o: any) => o.id === selectedOrgId.value)
    if (org) {
      try {
        const res = await fetch(`/api/organizations/${selectedOrgId.value}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            name: org.name,
            code: org.code,
            description: org.description,
            isActive: org.isActive,
            allowedEmailDomains: allowedDomains.value,
            workspaceIcon: workspaceIcon.value,
            workspaceCover: workspaceCover.value
          })
        })
        if (!res.ok) {
          hadFailure = true
          showError('Không thể cập nhật cấu hình không gian làm việc.')
        }
      } catch (e) {
        hadFailure = true
        showError('Không thể cập nhật cấu hình không gian làm việc.')
        console.warn("Lỗi cập nhật cấu hình không gian làm việc:", e)
      }
    }
  }

  // Update project settings
  if (canManageProjectWorkflow.value && selectedProjectId.value) {
    const proj = projects.value.find((p: any) => p.id === selectedProjectId.value)
    if (proj) {
      try {
        const res = await fetch(`/api/projects/${selectedProjectId.value}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            name: proj.name,
            code: proj.code,
            description: proj.description,
            logoUrl: proj.logoUrl,
            status: proj.status,
            startDate: proj.startDate,
            endDate: proj.endDate,
            organizationId: proj.organizationId,
            enableOnHold: enableOnHold.value,
            enableInReview: enableInReview.value,
            requireEvidenceToDone: requireEvidenceToDone.value,
            restrictTransitionsToAdmin: restrictTransitionsToAdmin.value
          })
        })
        if (!res.ok) {
          hadFailure = true
          showError('Không thể cập nhật cấu hình bảng công việc.')
        }
      } catch (e) {
        hadFailure = true
        showError('Không thể cập nhật cấu hình bảng công việc.')
        console.warn("Lỗi cập nhật cấu hình bảng công việc:", e)
      }
    }
  }
  
  try {
    const refreshed = await loadDashboard()
    if (!refreshed) {
      hadFailure = true
      showError('KhÃ´ng thá»ƒ lÃ m má»›i dá»¯ liá»‡u sau khi lÆ°u.')
    }
  } catch (e) {
    hadFailure = true
    showError('Không thể làm mới dữ liệu sau khi lưu.')
  }
  
  isSaving.value = false
  if (!hadFailure) {
    showSuccess("Đã lưu tất cả cấu hình thành công!")
  }
}

onMounted(async () => {
  await loadSettings()
  applyAccent(accentColor.value, currentTheme.value as 'light' | 'dark')
  if (activeTab.value === 'logs') {
    fetchAuditLogs()
  }
})

function handleTabChange(tab: string) {
  if (tab === 'workflow' && !canManageProjectWorkflow.value) {
    return
  }
  activeTab.value = tab
  if (tab === 'logs') {
    fetchAuditLogs()
  }
}

const userInitials = computed(() => {
  const n = currentUser.value?.fullName || currentUser.value?.email || 'Qaly user'
  return n
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part: string) => part[0]?.toUpperCase() ?? '')
    .join('')
})
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main settings-layout no-scrollbar">
      <div class="settings-container">
        
        <!-- Header -->
        <header class="settings-header glass-card">
          <div class="profile-summary">
            <div class="profile-avatar settings-avatar">{{ userInitials }}</div>
            <div class="profile-meta">
              <span>Settings Portal</span>
              <h2>Cấu hình & Tùy chỉnh</h2>
              <p>{{ currentUser?.fullName || currentUser?.email || 'Qaly User' }} &mdash; <strong class="role-badge">{{ displayRole(currentUser?.role) }}</strong></p>
            </div>
          </div>
          <button class="primary-button" @click="saveSettings" :disabled="isSaving">
            <Save :size="16" />
            <span>{{ isSaving ? 'Đang lưu...' : 'Lưu cài đặt' }}</span>
          </button>
        </header>

        <!-- Main Body -->
        <div class="settings-body">
          <!-- Sidebar Tabs -->
          <nav class="settings-nav glass-card" aria-label="Settings sections">
            <button 
              class="settings-nav-item" 
              :class="{ 'is-active': activeTab === 'profile' }" 
              @click="handleTabChange('profile')"
            >
              <User :size="18" />
              <span>Hồ sơ cá nhân</span>
            </button>
            <button 
              class="settings-nav-item" 
              :class="{ 'is-active': activeTab === 'appearance' }" 
              @click="handleTabChange('appearance')"
            >
              <Palette :size="18" />
              <span>Giao diện & Chủ đề</span>
            </button>
            <button 
              v-if="canManageProjectWorkflow"
              class="settings-nav-item" 
              :class="{ 'is-active': activeTab === 'workflow' }" 
              @click="handleTabChange('workflow')"
            >
              <Sliders :size="18" />
              <span>Bảng công việc (Jira Board)</span>
            </button>
            <button 
              class="settings-nav-item" 
              :class="{ 'is-active': activeTab === 'notifications' }" 
              @click="handleTabChange('notifications')"
            >
              <Bell :size="18" />
              <span>Thông báo</span>
            </button>
            <button 
              class="settings-nav-item" 
              :class="{ 'is-active': activeTab === 'apikeys' }" 
              @click="handleTabChange('apikeys')"
            >
              <Key :size="18" />
              <span>API Keys & Tích hợp</span>
            </button>
            <button 
              class="settings-nav-item" 
              :class="{ 'is-active': activeTab === 'privacy' }"
              @click="handleTabChange('privacy')"
            >
              <Shield :size="18" />
              <span>Quyền riêng tư & dữ liệu</span>
            </button>
            <button
              class="settings-nav-item"
              :class="{ 'is-active': activeTab === 'logs' }" 
              @click="handleTabChange('logs')"
            >
              <Activity :size="18" />
              <span>Nhật ký hoạt động</span>
            </button>
          </nav>

          <!-- Panels Content -->
          <main class="settings-content">
            
            <!-- Tab: Profile -->
            <section v-if="activeTab === 'profile'" class="settings-panel glass-card reveal">
              <div class="panel-section-header">
                <User :size="20" class="icon-primary" />
                <h3>Thông tin tài khoản</h3>
              </div>
              <div class="input-grid">
                <div class="form-group">
                  <label for="profile-name">Họ và tên</label>
                  <input id="profile-name" v-model="name" type="text" placeholder="Họ và tên người dùng" />
                </div>
                <div class="form-group">
                  <label for="profile-email">Địa chỉ Email</label>
                  <input id="profile-email" :value="email" type="email" disabled title="Không thể thay đổi email" />
                  <span class="help-text">Email được khóa cố định theo tài khoản hệ thống.</span>
                </div>
                <div class="form-group">
                  <label for="profile-lang">Ngôn ngữ hiển thị</label>
                  <select id="profile-lang" v-model="selectedLang">
                    <option value="vi">Tiếng Việt (Vietnamese)</option>
                    <option value="en">English (Tiếng Anh)</option>
                  </select>
                </div>
                <div class="form-group">
                  <label for="profile-timeout">Thời gian tự động đăng xuất</label>
                  <select id="profile-timeout" v-model="sessionTimeout">
                    <option :value="15">15 phút</option>
                    <option :value="30">30 phút</option>
                    <option :value="60">60 phút (1 giờ)</option>
                    <option :value="180">180 phút (3 giờ)</option>
                    <option :value="0">Không bao giờ</option>
                  </select>
                </div>
              </div>

              <!-- Breakthrough: Notion-Style Workspace Branding & Security -->
              <div v-if="canManageWorkspaceSettings" class="sub-section border-top">
                <div class="panel-section-header">
                  <Globe :size="18" class="icon-success" />
                  <h3>Cấu hình Không gian làm việc (Notion Workspace style)</h3>
                </div>
                <p class="panel-desc">Tùy biến thương hiệu không gian làm việc và thắt chặt bảo mật gia nhập đội ngũ.</p>
                
                <div class="input-grid">
                  <div v-if="organizations.length > 0" class="form-group" style="grid-column: 1 / -1; margin-bottom: 8px;">
                    <label for="ws-org-select">Chọn tổ chức/không gian để cấu hình</label>
                    <select id="ws-org-select" v-model="selectedOrgId" @change="loadOrgSettings">
                      <option v-for="org in organizations" :key="org.id" :value="org.id">
                        {{ org.name }}
                      </option>
                    </select>
                  </div>
                  <div class="form-group">
                    <label for="ws-domains">Email Domains được phép gia nhập</label>
                    <input id="ws-domains" v-model="allowedDomains" type="text" placeholder="company.com, domain.vn" />
                    <span class="help-text">Tách biệt bằng dấu phẩy. Chỉ cho phép các email đuôi này tự đăng ký thành viên.</span>
                  </div>
                  <div class="form-group">
                    <label for="ws-icon">Biểu tượng không gian (Emoji)</label>
                    <div style="display: flex; gap: 8px; align-items: center;">
                      <span class="emoji-preview-box">{{ workspaceIcon }}</span>
                      <select id="ws-icon" v-model="workspaceIcon" style="flex: 1;">
                        <option value="🚀">🚀 Tên lửa</option>
                        <option value="💡">💡 Ý tưởng</option>
                        <option value="🎨">🎨 Sáng tạo</option>
                        <option value="🛡️">🛡️ Bảo mật</option>
                        <option value="💻">💻 Công nghệ</option>
                        <option value="📈">📈 Tăng trưởng</option>
                      </select>
                    </div>
                  </div>
                  <div class="form-group" style="grid-column: 1 / -1;">
                    <label>Ảnh bìa không gian (Workspace Cover)</label>
                    <div class="cover-selector-grid">
                      <button 
                        class="cover-opt sunset" 
                        :class="{ 'is-active': workspaceCover === 'sunset' }" 
                        @click="workspaceCover = 'sunset'"
                        type="button"
                      >
                        Sunset Glow
                      </button>
                      <button 
                        class="cover-opt emerald-sea" 
                        :class="{ 'is-active': workspaceCover === 'emerald-sea' }" 
                        @click="workspaceCover = 'emerald-sea'"
                        type="button"
                      >
                        Emerald Sea
                      </button>
                      <button 
                        class="cover-opt deep-ocean" 
                        :class="{ 'is-active': workspaceCover === 'deep-ocean' }" 
                        @click="workspaceCover = 'deep-ocean'"
                        type="button"
                      >
                        Deep Ocean
                      </button>
                      <button 
                        class="cover-opt amethyst-nebula" 
                        :class="{ 'is-active': workspaceCover === 'amethyst-nebula' }" 
                        @click="workspaceCover = 'amethyst-nebula'"
                        type="button"
                      >
                        Amethyst Nebula
                      </button>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Change Password Sub-section -->
              <div class="sub-section border-top">
                <div class="panel-section-header">
                  <Lock :size="18" class="icon-warning" />
                  <h3>Đổi mật khẩu tài khoản</h3>
                </div>
                <form @submit.prevent="changePassword" class="password-form-inputs">
                  <div class="form-group">
                    <label for="curr-password">Mật khẩu hiện tại</label>
                    <input id="curr-password" v-model="passwordForm.currentPassword" type="password" required />
                  </div>
                  <div class="form-group">
                    <label for="new-password">Mật khẩu mới</label>
                    <input id="new-password" v-model="passwordForm.newPassword" type="password" required />
                  </div>
                  <div class="form-group">
                    <label for="confirm-new-password">Xác nhận mật khẩu mới</label>
                    <input id="confirm-new-password" v-model="passwordForm.confirmNewPassword" type="password" required />
                  </div>
                  <div class="form-actions" style="margin-top: 16px;">
                    <button type="submit" class="primary-button primary-button--compact" :disabled="isSavingPassword">
                      <RefreshCw :size="14" :class="{ 'animate-spin': isSavingPassword }" />
                      <span>{{ isSavingPassword ? 'Đang đổi...' : 'Cập nhật mật khẩu' }}</span>
                    </button>
                  </div>
                </form>
              </div>
            </section>

            <!-- Tab: Appearance -->
            <section v-if="activeTab === 'appearance'" class="settings-panel glass-card reveal">
              <div class="panel-section-header">
                <Palette :size="20" class="icon-primary" />
                <h3>Giao diện & Màu sắc</h3>
              </div>
              <p class="panel-desc">Cá nhân hóa không gian làm việc của bạn để nâng cao năng suất và giảm mỏi mắt.</p>

              <!-- Dark mode toggler -->
              <div class="setting-row">
                <div class="setting-info">
                  <strong>Chế độ tối (Dark Mode)</strong>
                  <p>Tự động điều chỉnh giao diện dựa trên tùy chọn hệ thống hoặc ép buộc giao diện.</p>
                </div>
                <button class="theme-toggle-btn" @click="toggleTheme">
                  <Sun v-if="currentTheme === 'dark'" :size="18" />
                  <Moon v-else :size="18" />
                  <span>{{ currentTheme === 'dark' ? 'Chế độ Sáng' : 'Chế độ Tối' }}</span>
                </button>
              </div>

              <!-- Accent color selection -->
              <div class="setting-row-vertical">
                <div class="setting-info">
                  <strong>Tông màu chủ đạo (Accent Color)</strong>
                  <p>Chọn màu sắc hiển thị cho các nút bấm, liên kết và trạng thái active trong toàn bộ hệ thống.</p>
                </div>
                <div class="color-picker-grid">
                  <button 
                    class="color-option sapphire" 
                    :class="{ 'is-selected': accentColor === 'sapphire' }" 
                    @click="selectAccent('sapphire')"
                    title="Sapphire Blue"
                  >
                    <span class="color-preview" style="background: #3b82f6;"></span>
                    <span>Sapphire Blue</span>
                    <Check v-if="accentColor === 'sapphire'" :size="14" class="selected-check" />
                  </button>
                  <button 
                    class="color-option emerald" 
                    :class="{ 'is-selected': accentColor === 'emerald' }" 
                    @click="selectAccent('emerald')"
                    title="Pine Emerald"
                  >
                    <span class="color-preview" style="background: #10b981;"></span>
                    <span>Pine Emerald</span>
                    <Check v-if="accentColor === 'emerald'" :size="14" class="selected-check" />
                  </button>
                  <button 
                    class="color-option amethyst" 
                    :class="{ 'is-selected': accentColor === 'amethyst' }" 
                    @click="selectAccent('amethyst')"
                    title="Amethyst Purple"
                  >
                    <span class="color-preview" style="background: #8b5cf6;"></span>
                    <span>Amethyst Purple</span>
                    <Check v-if="accentColor === 'amethyst'" :size="14" class="selected-check" />
                  </button>
                  <button 
                    class="color-option rose" 
                    :class="{ 'is-selected': accentColor === 'rose' }" 
                    @click="selectAccent('rose')"
                    title="Crimson Rose"
                  >
                    <span class="color-preview" style="background: #f43f5e;"></span>
                    <span>Crimson Rose</span>
                    <Check v-if="accentColor === 'rose'" :size="14" class="selected-check" />
                  </button>
                  <button 
                    class="color-option amber" 
                    :class="{ 'is-selected': accentColor === 'amber' }" 
                    @click="selectAccent('amber')"
                    title="Sunset Amber"
                  >
                    <span class="color-preview" style="background: #f59e0b;"></span>
                    <span>Sunset Amber</span>
                    <Check v-if="accentColor === 'amber'" :size="14" class="selected-check" />
                  </button>
                </div>
              </div>
            </section>

            <!-- Breakthrough: Tab: Jira-style Kanban Workflow Builder -->
            <section v-if="activeTab === 'workflow' && canManageProjectWorkflow" class="settings-panel glass-card reveal">
              <div class="panel-section-header">
                <Sliders :size="20" class="icon-primary" />
                <h3>Cấu hình Bảng công việc & Workflow (Jira style)</h3>
              </div>
              <p class="panel-desc">Điều chỉnh các cột trạng thái trên bảng Kanban và thiết lập luật di chuyển thẻ (State Transitions).</p>

              <div v-if="projects.length > 0" class="form-group" style="margin-bottom: 24px;">
                <label for="settings-project-select">Chọn dự án để cấu hình</label>
                <select id="settings-project-select" v-model="selectedProjectId" @change="loadProjectSettings" style="width: 100%; border: 1px solid var(--line); border-radius: var(--qaly-radius-lg); padding: 10px 14px; background: var(--panel-soft); color: var(--text);">
                  <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.name }}</option>
                </select>
              </div>

              <div class="workflow-visualizer-card glass-card">
                <strong>Sơ đồ phân cột Kanban hiện tại</strong>
                <div class="kanban-cols-flow">
                  <div class="flow-col">Todo</div>
                  <div class="flow-arrow">&rarr;</div>
                  <div class="flow-col">InProgress</div>
                  <div class="flow-arrow">&rarr;</div>
                  <div class="flow-col" v-if="enableOnHold" :class="{ 'disabled-col': !enableOnHold }">OnHold</div>
                  <div class="flow-arrow" v-if="enableOnHold">&rarr;</div>
                  <div class="flow-col" v-if="enableInReview" :class="{ 'disabled-col': !enableInReview }">InReview</div>
                  <div class="flow-arrow" v-if="enableInReview">&rarr;</div>
                  <div class="flow-col">Done</div>
                </div>
              </div>

              <div class="checkbox-settings-list mt-24">
                <label class="setting-checkbox-row">
                  <input type="checkbox" v-model="enableOnHold" />
                  <div class="checkbox-info">
                    <strong>Kích hoạt cột Tạm dừng (On Hold)</strong>
                    <p>Cho phép các nhiệm vụ chuyển trạng thái tạm dừng chờ phản hồi bên thứ ba.</p>
                  </div>
                </label>

                <label class="setting-checkbox-row">
                  <input type="checkbox" v-model="enableInReview" />
                  <div class="checkbox-info">
                    <strong>Kích hoạt cột Đang duyệt (In Review)</strong>
                    <p>Kích hoạt luồng duyệt minh chứng. Nhiệm vụ sẽ đi qua duyệt của Manager trước khi sang Done.</p>
                  </div>
                </label>

                <label class="setting-checkbox-row">
                  <input type="checkbox" v-model="requireEvidenceToDone" />
                  <div class="checkbox-info">
                    <strong>Ràng buộc tệp minh chứng khi hoàn thành (Require Evidence)</strong>
                    <p>Yêu cầu người thực hiện bắt buộc phải đính kèm ít nhất 1 tệp tin (Evidence) mới có thể chuyển trạng thái sang Done.</p>
                  </div>
                </label>

                <label class="setting-checkbox-row">
                  <input type="checkbox" v-model="restrictTransitionsToAdmin" />
                  <div class="checkbox-info">
                    <strong>Giới hạn quyền duyệt Done cho Trưởng nhóm/Admin</strong>
                    <p>Chỉ Quản lý dự án (Manager) hoặc Admin mới được phép kéo thả nhiệm vụ sang Done.</p>
                  </div>
                </label>
              </div>
            </section>

            <!-- Tab: Notifications -->
            <section v-if="activeTab === 'notifications'" class="settings-panel glass-card reveal">
              <div class="panel-section-header">
                <Bell :size="20" class="icon-primary" />
                <h3>Tùy chỉnh thông báo</h3>
              </div>
              <p class="panel-desc">Kiểm soát các loại thông báo bạn nhận được qua email hoặc ứng dụng.</p>

              <div class="checkbox-settings-list">
                <label class="setting-checkbox-row">
                  <input type="checkbox" v-model="notifEmailTask" />
                  <div class="checkbox-info">
                    <strong>Thông báo Email về nhiệm vụ</strong>
                    <p>Nhận thư thông báo ngay khi có dự án hoặc nhiệm vụ mới được giao cho bạn.</p>
                  </div>
                </label>

                <label class="setting-checkbox-row">
                  <input type="checkbox" v-model="notifPushMention" />
                  <div class="checkbox-info">
                    <strong>Thông báo đẩy khi được nhắc đến (@mention)</strong>
                    <p>Nhận thông báo màn hình/trình duyệt khi đồng nghiệp nhắc đến bạn trong các thảo luận.</p>
                  </div>
                </label>

                <label class="setting-checkbox-row">
                  <input type="checkbox" v-model="notifSoundMeeting" />
                  <div class="checkbox-info">
                    <strong>Phát âm thanh khi có cuộc họp nhóm bắt đầu</strong>
                    <p>Phát chuông thông báo trực quan khi có thành viên trong nhóm bắt đầu cuộc họp phòng ảo.</p>
                  </div>
                </label>

                <label class="setting-checkbox-row">
                  <input type="checkbox" v-model="notifWeeklyReport" />
                  <div class="checkbox-info">
                    <strong>Báo cáo tóm tắt hiệu suất cuối tuần</strong>
                    <p>Hệ thống AI sẽ gửi email tóm tắt tiến trình và công việc trễ hạn vào mỗi chiều thứ Sáu.</p>
                  </div>
                </label>
              </div>
            </section>

            <!-- Tab: ApiKeys & Webhooks -->
            <section v-if="activeTab === 'apikeys'" class="apikeys-panel reveal">
              <!-- Re-use the existing ApiKeysTab component -->
              <ApiKeysTab />

              <!-- Info about organization level Webhooks -->
              <div class="glass-card mt-24" style="padding: 24px; margin-top: 24px;">
                <div class="panel-section-header">
                  <Globe :size="20" class="icon-success" />
                  <h3>Tích hợp Webhooks Hệ thống</h3>
                </div>
                <p class="panel-desc" style="margin-bottom: 16px;">Để định cấu hình Webhook nhận cập nhật tự động từ dự án, hãy đi tới <strong>Trang chi tiết dự án &rarr; tab Webhook</strong> để thiết lập riêng cho từng dự án.</p>
                <div class="webhook-info-banner">
                  <ShieldAlert :size="18" />
                  <span>Webhook cho phép các ứng dụng bên thứ ba (như Slack, Discord, hoặc máy chủ của bạn) đăng ký nhận các sự kiện thời gian thực khi có thay đổi trong QALY.</span>
                </div>
              </div>
            </section>

            <section v-if="activeTab === 'privacy'" class="settings-panel glass-card reveal">
              <PrivacySettingsTab />
            </section>

            <!-- Tab: Audit Logs -->
            <section v-if="activeTab === 'logs'" class="settings-panel glass-card reveal">
              <div class="panel-section-header">
                <Activity :size="20" class="icon-primary" />
                <h3>Nhật ký hoạt động cá nhân</h3>
              </div>
              <p class="panel-desc">Xem toàn bộ lịch sử các thao tác thay đổi dữ liệu, đăng nhập và tác vụ bạn đã thực hiện trong thời gian qua.</p>

              <!-- Loading spinner -->
              <div v-if="isLoadingLogs" class="logs-loading">
                <RefreshCw class="animate-spin text-primary" :size="28" />
                <span>Đang tải lịch sử hoạt động...</span>
              </div>

              <!-- Logs table -->
              <div v-else class="logs-table-wrapper">
                <table class="logs-table">
                  <thead>
                    <tr>
                      <th>Thời gian</th>
                      <th>Thao tác</th>
                      <th>Loại dữ liệu</th>
                      <th>Đối tượng (ID)</th>
                      <th>Địa chỉ IP</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="log in auditLogs" :key="log.id">
                      <td class="log-time">{{ new Date(log.timestamp).toLocaleString() }}</td>
                      <td>
                        <span class="action-tag" :class="log.action.toLowerCase()">
                          {{ log.action }}
                        </span>
                      </td>
                      <td><code>{{ log.entityType }}</code></td>
                      <td class="log-entity-id" :title="log.entityId">
                        {{ log.entityId.substring(0, 8) }}...
                      </td>
                      <td><span class="ip-address">{{ log.ipAddress || 'Internal' }}</span></td>
                    </tr>
                    <tr v-if="auditLogs.length === 0">
                      <td colspan="5" class="empty-logs">Không tìm thấy bản ghi hoạt động nào.</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </section>

          </main>
        </div>

      </div>
    </div>
  </div>
</template>

<style scoped>
.settings-layout {
  padding: 24px;
  background: var(--surface-warm);
}

.settings-container {
  max-width: 1100px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.settings-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 24px;
  background: var(--panel);
  border-radius: var(--radius-panel);
}

.profile-summary {
  display: flex;
  align-items: center;
  gap: 20px;
}

.settings-avatar {
  width: 64px;
  height: 64px;
  font-size: 24px;
  font-weight: 800;
  background: linear-gradient(135deg, var(--primary), var(--primary-strong));
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  box-shadow: var(--qaly-shadow-md);
}

.profile-meta span {
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--primary);
  font-weight: 700;
}

.profile-meta h2 {
  font-size: 22px;
  font-weight: 800;
  margin: 2px 0;
  color: var(--text-strong);
}

.profile-meta p {
  font-size: 13px;
  color: var(--muted);
  margin: 0;
}

.role-badge {
  font-size: 11px;
  background: var(--primary-soft);
  color: var(--primary);
  padding: 2px 8px;
  border-radius: var(--radius-pill);
}

.settings-body {
  display: grid;
  grid-template-columns: 260px 1fr;
  gap: 24px;
}

@media (max-width: 768px) {
  .settings-body {
    grid-template-columns: 1fr;
  }
}

.settings-nav {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 16px;
  height: fit-content;
  background: var(--panel);
}

.settings-nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border-radius: var(--qaly-radius-lg);
  color: var(--muted);
  font-size: 14px;
  font-weight: 600;
  background: transparent;
  border: none;
  cursor: pointer;
  text-align: left;
  transition: all 0.2s ease;
}

.settings-nav-item:hover {
  background: var(--primary-soft);
  color: var(--primary);
}

.settings-nav-item.is-active {
  background: var(--primary);
  color: white;
}

.settings-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.settings-panel {
  padding: 32px;
  background: var(--panel);
}

.panel-section-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 24px;
}

.panel-section-header h3 {
  font-size: 16px;
  font-weight: 800;
  margin: 0;
  color: var(--text-strong);
}

.panel-desc {
  font-size: 13.5px;
  color: var(--muted);
  margin-top: -12px;
  margin-bottom: 28px;
}

.input-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

@media (max-width: 640px) {
  .input-grid {
    grid-template-columns: 1fr;
  }
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group label {
  font-size: 12.5px;
  font-weight: 700;
  color: var(--text-strong);
}

.form-group input,
.form-group select {
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  padding: 10px 14px;
  outline: none;
  font-size: 13.5px;
  background: var(--panel-soft);
  color: var(--text);
  transition: border-color 0.2s;
}

.form-group input:focus,
.form-group select:focus {
  border-color: var(--primary);
}

.form-group input:disabled {
  background: rgba(148, 163, 184, 0.08);
  color: var(--muted);
  cursor: not-allowed;
}

.help-text {
  font-size: 11px;
  color: var(--muted);
  margin-top: -2px;
}

.emoji-preview-box {
  font-size: 24px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  padding: 6px 12px;
}

.cover-selector-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 10px;
}

@media (max-width: 640px) {
  .cover-selector-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

.cover-opt {
  height: 54px;
  border-radius: var(--qaly-radius-lg);
  border: 2px solid transparent;
  color: white;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s;
  text-shadow: 0 1px 3px rgba(0,0,0,0.5);
}

.cover-opt.sunset { background: linear-gradient(135deg, #f97316, #ef4444); }
.cover-opt.emerald-sea { background: linear-gradient(135deg, #34d399, #059669); }
.cover-opt.deep-ocean { background: linear-gradient(135deg, #0f52ba, #0f172a); }
.cover-opt.amethyst-nebula { background: linear-gradient(135deg, #a78bfa, #6d28d9); }

.cover-opt.is-active {
  border-color: #ffffff;
  box-shadow: 0 0 0 2px var(--primary);
}

.workflow-visualizer-card {
  padding: 20px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: var(--qaly-radius-lg);
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 20px;
}

.workflow-visualizer-card strong {
  font-size: 13.5px;
  color: var(--text-strong);
}

.kanban-cols-flow {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.flow-col {
  padding: 6px 12px;
  background: var(--primary-soft);
  color: var(--primary);
  border: 1px solid rgba(15, 82, 186, 0.2);
  border-radius: 6px;
  font-weight: 700;
  font-size: 12px;
}

.flow-arrow {
  color: var(--muted);
  font-weight: 800;
}

.disabled-col {
  background: rgba(148, 163, 184, 0.08);
  color: var(--muted);
  border-color: var(--line);
  text-decoration: line-through;
  opacity: 0.5;
}

.sub-section {
  margin-top: 32px;
  padding-top: 24px;
}

.border-top {
  border-top: 1px solid var(--line);
}

.password-form-inputs {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 16px;
}

@media (max-width: 900px) {
  .password-form-inputs {
    grid-template-columns: 1fr;
  }
}

.setting-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 0;
  border-bottom: 1px solid var(--line-light);
}

.setting-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.setting-info strong {
  font-size: 14px;
  color: var(--text-strong);
}

.setting-info p {
  font-size: 12.5px;
  color: var(--muted);
  margin: 0;
}

.theme-toggle-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid var(--line);
  background: var(--panel-soft);
  color: var(--text);
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s;
}

.theme-toggle-btn:hover {
  background: var(--primary-soft);
  color: var(--primary);
  border-color: var(--primary);
}

.setting-row-vertical {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px 0;
}

.color-picker-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 12px;
}

.color-option {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid var(--line);
  background: var(--panel-soft);
  cursor: pointer;
  font-weight: 600;
  font-size: 13.5px;
  color: var(--text);
  transition: all 0.2s ease;
  position: relative;
}

.color-option:hover {
  border-color: var(--primary);
}

.color-option.is-selected {
  border-color: var(--primary);
  background: var(--primary-soft);
  color: var(--primary);
}

.color-preview {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.selected-check {
  margin-left: auto;
  color: var(--primary);
}

.checkbox-settings-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.setting-checkbox-row {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  padding: 16px;
  border-radius: var(--qaly-radius-lg);
  background: var(--panel-soft);
  cursor: pointer;
  transition: background 0.2s;
}

.setting-checkbox-row:hover {
  background: rgba(148, 163, 184, 0.06);
}

.setting-checkbox-row input[type="checkbox"] {
  width: 18px;
  height: 18px;
  border-radius: 4px;
  accent-color: var(--primary);
  margin-top: 2px;
}

.checkbox-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.checkbox-info strong {
  font-size: 14px;
  color: var(--text-strong);
}

.checkbox-info p {
  font-size: 12.5px;
  color: var(--muted);
  margin: 0;
}

.icon-primary { color: var(--primary); }
.icon-warning { color: var(--warning); }
.icon-success { color: var(--success); }

.mt-24 { margin-top: 24px; }

.webhook-info-banner {
  display: flex;
  gap: 12px;
  background: rgba(148, 163, 184, 0.05);
  border: 1px solid var(--line);
  padding: 14px;
  border-radius: var(--qaly-radius-lg);
  font-size: 12.5px;
  color: var(--muted);
}

.webhook-info-banner span {
  line-height: 1.5;
}

.logs-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 64px 0;
  gap: 12px;
  color: var(--muted);
}

.logs-table-wrapper {
  overflow-x: auto;
}

.logs-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  text-align: left;
}

.logs-table th,
.logs-table td {
  padding: 12px 16px;
  border-bottom: 1px solid var(--line-light);
}

.logs-table th {
  font-weight: 700;
  color: var(--muted);
  background: var(--panel-soft);
}

.log-time {
  font-family: monospace;
  color: var(--muted);
  white-space: nowrap;
}

.action-tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
}

.action-tag.create { background: rgba(16, 185, 129, 0.1); color: #059669; }
.action-tag.update { background: rgba(59, 130, 246, 0.1); color: #2563eb; }
.action-tag.delete { background: rgba(239, 68, 68, 0.1); color: #dc2626; }
.action-tag.login { background: rgba(139, 92, 246, 0.1); color: #7c3aed; }

.log-entity-id {
  font-family: monospace;
  color: var(--muted);
}

.ip-address {
  font-family: monospace;
  color: var(--muted);
}

.empty-logs {
  text-align: center;
  padding: 32px 0;
  color: var(--muted);
  font-style: italic;
}
</style>
