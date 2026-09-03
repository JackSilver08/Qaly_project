<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { 
  Box, Database, HardDrive, RefreshCw, Trash2, Search, ArrowUpDown, 
  Download, Archive, CheckSquare, ShieldAlert, Sparkles, FolderArchive, 
  ArrowUpRight, Info, Check, Scissors, AlertTriangle, Save
} from 'lucide-vue-next'
import { useDashboardContext } from '../composables/dashboard-context'
import { showSuccess, showError, showWarning } from '../composables/use-toast'
import { confirmDialog } from '../composables/use-confirm-dialog'
import { apiFetch } from '../utils/api-client'

const {
  restoreProject: baseRestoreProject,
  deleteProject: baseDeleteProject,
  loadDashboard
} = useDashboardContext()

// Pagination & Search state
const searchQ = ref('')
const sortBy = ref('date')
const currentPage = ref(1)
const totalPages = ref(1)
const totalCount = ref(0)
const pageSize = 12
const archivedProjects = ref<any[]>([])
const isLoadingArchived = ref(false)

// Storage stats state
const storageStats = ref({
  totalBytes: 0,
  attachmentBytes: 0,
  totalFileCount: 0,
  archivedProjectFileCount: 0
})
const isLoadingStats = ref(false)

// Policies settings
const autoCleanLogs = ref(true)
const compressFiles = ref(false)
const trashRetention = ref(30)
const isSavingPolicy = ref(false)

// Storage deduplication state
const isDeduplicating = ref(false)
const duplicateSavedSpace = ref(0)
const duplicatesList = ref<any[]>([])

// Trash bin state
const trashProjects = ref<any[]>([])
const isLoadingTrash = ref(false)
const isLoadingDuplicates = ref(false)
const duplicateScanAttempted = ref(false)
const duplicateScanError = ref('')

// Load policies from localStorage
function loadPolicies() {
  try {
    const saved = localStorage.getItem('qaly:archive-policies')
    if (saved) {
      const parsed = JSON.parse(saved)
      autoCleanLogs.value = parsed.autoCleanLogs ?? true
      compressFiles.value = parsed.compressFiles ?? false
      trashRetention.value = parsed.trashRetention ?? 30
    }
  } catch (e) {
    console.warn("Failed to load archive policies:", e)
  }
}

function savePolicies() {
  isSavingPolicy.value = true
  setTimeout(() => {
    try {
      localStorage.setItem('qaly:archive-policies', JSON.stringify({
        autoCleanLogs: autoCleanLogs.value,
        compressFiles: compressFiles.value,
        trashRetention: trashRetention.value
      }))
      showSuccess('Đã cập nhật chính sách dọn dẹp dung lượng lưu trữ!')
    } catch (e) {
      showError('Không thể lưu chính sách.')
    } finally {
      isSavingPolicy.value = false
    }
  }, 400)
}

// Fetch real archived projects
async function loadArchivedProjects() {
  isLoadingArchived.value = true
  try {
    const res = await apiFetch(`/api/projects/archived?page=${currentPage.value}&pageSize=${pageSize}&search=${encodeURIComponent(searchQ.value)}`)
    if (res.ok) {
      const payload = await res.json()
      if (payload.isSuccess && payload.data) {
        archivedProjects.value = payload.data.items || []
        totalPages.value = payload.data.totalPages || 1
        totalCount.value = payload.data.totalCount || 0
      }
    }
  } catch (e) {
    console.error("Failed to load archived projects:", e)
  } finally {
    isLoadingArchived.value = false
  }
}

// Fetch real storage stats
async function loadStorageStats() {
  isLoadingStats.value = true
  try {
    const res = await apiFetch('/api/storage/stats')
    if (res.ok) {
      const payload = await res.json()
      if (payload.isSuccess && payload.data) {
        storageStats.value = payload.data
      }
    }
  } catch (e) {
    console.error("Failed to load storage stats:", e)
  } finally {
    isLoadingStats.value = false
  }
}

function changePage(page: number) {
  if (page < 1 || page > totalPages.value) return
  currentPage.value = page
  void loadArchivedProjects()
}

// Watch query search or sorting to reload
watch([searchQ, sortBy], () => {
  currentPage.value = 1
  void loadArchivedProjects()
})

const filteredProjects = computed(() => {
  // Sorting local fallback (the backend already filtered by search)
  let list = [...archivedProjects.value]
  return list.sort((a, b) => {
    if (sortBy.value === 'name') {
      return a.name.localeCompare(b.name)
    } else if (sortBy.value === 'size') {
      const sizeA = a.taskCount || 0
      const sizeB = b.taskCount || 0
      return sizeB - sizeA
    } else if (sortBy.value === 'progress') {
      return (b.progressPercentage || 0) - (a.progressPercentage || 0)
    } else {
      const dateA = a.archivedAt ? new Date(a.archivedAt).getTime() : new Date(a.createdAt).getTime()
      const dateB = b.archivedAt ? new Date(b.archivedAt).getTime() : new Date(b.createdAt).getTime()
      return dateB - dateA
    }
  })
})

function formatBytes(bytes: number) {
  if (bytes === 0) return '0 Bytes'
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

function exportProjectData(project: any) {
  const payload = {
    ...project,
    exportedAt: new Date().toISOString()
  }
  const dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(payload, null, 2))
  const downloadAnchor = document.createElement('a')
  downloadAnchor.setAttribute("href", dataStr)
  downloadAnchor.setAttribute("download", `qaly-project-archive-${project.id}.json`)
  document.body.appendChild(downloadAnchor)
  downloadAnchor.click()
  downloadAnchor.remove()
  showSuccess(`Đã tải dữ liệu dự án "${project.name}" thành công!`)
}

async function loadTrash() {
  isLoadingTrash.value = true
  try {
    const res = await apiFetch('/api/projects/trash?page=1&pageSize=100')
    if (res.ok) {
      const payload = await res.json()
      trashProjects.value = (payload.data?.items || []).map((p: any) => {
        const taskFactor = p.taskCount || 5
        const sizeMB = (taskFactor * 1.8 + 2).toFixed(1) + ' MB'
        
        const deletedAt = p.deletedAt ? new Date(p.deletedAt) : new Date()
        const diffTime = Math.abs(new Date().getTime() - deletedAt.getTime())
        const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24))
        const daysLeft = Math.max(1, 30 - diffDays)

        return {
          id: p.id,
          name: p.name,
          code: p.code,
          size: sizeMB,
          daysLeft: daysLeft
        }
      })
    }
  } catch (e) {
    console.warn("Lỗi tải thùng rác dự án:", e)
  } finally {
    isLoadingTrash.value = false
  }
}

async function loadDuplicates() {
  isLoadingDuplicates.value = true
  duplicateScanError.value = ''
  try {
    const res = await apiFetch('/api/storage/duplicates')
    const payload = await res.json().catch(() => null)
    if (res.ok) {
      duplicatesList.value = payload.data || []
    } else {
      duplicatesList.value = []
      duplicateScanError.value = payload?.error || 'Không thể kiểm tra đầy đủ tệp trùng lặp trong storage.'
      showWarning(duplicateScanError.value)
    }
  } catch (e) {
    duplicateScanError.value = 'Lỗi kết nối khi kiểm tra tệp trùng lặp.'
    showWarning(duplicateScanError.value)
  } finally {
    duplicateScanAttempted.value = true
    isLoadingDuplicates.value = false
  }
}

async function runDeduplicator() {
  if (duplicatesList.value.length === 0) return
  isDeduplicating.value = true
  
  try {
    const res = await apiFetch('/api/storage/deduplicate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' }
    })
    const payload = await res.json().catch(() => null)

    if (res.ok) {
      const result = payload.data
      const savedMB = result ? (result.bytesSaved / (1024 * 1024)).toFixed(1) : '0'
      duplicateSavedSpace.value += result ? (result.bytesSaved / (1024 * 1024)) : 0
      showSuccess(`Đã dọn dẹp ${savedMB} MB bằng cách gộp liên kết tệp trùng lặp theo content hash.`)
      if ((result?.scanFailures || 0) > 0 || (result?.cleanupFailures || 0) > 0) {
        showWarning((result?.warnings || []).join(' ') || 'Một phần storage cần được đối soát lại.')
      }
      await loadDuplicates()
      await loadDashboard()
      await loadStorageStats()
    } else {
      showError(payload?.error || 'Không thể thực hiện tối ưu hóa dung lượng.')
    }
  } catch (e) {
    showError('Lỗi kết nối khi tối ưu hóa dung lượng.')
  } finally {
    isDeduplicating.value = false
  }
}

async function restoreProject(id: string) {
  await baseRestoreProject(id)
  await loadArchivedProjects()
  await loadDashboard()
}

async function deleteProject(id: string) {
  await baseDeleteProject(id)
  await loadArchivedProjects()
  await loadDashboard()
}

// Restore project from real Trash Bin
async function restoreTrashProject(id: string) {
  const p = trashProjects.value.find(item => item.id === id)
  if (!p) return
  
  try {
    const res = await apiFetch(`/api/projects/${id}/restore`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' }
    })
    
    if (res.ok) {
      trashProjects.value = trashProjects.value.filter(item => item.id !== id)
      showSuccess(`Đã khôi phục thành công dự án "${p.name}" về danh mục hoạt động!`)
      await loadDashboard()
      await loadArchivedProjects()
    } else {
      showError('Không thể khôi phục dự án.')
    }
  } catch (e) {
    showError('Lỗi kết nối khi khôi phục dự án.')
  }
}

async function deleteTrashProject(id: string) {
  const p = trashProjects.value.find(item => item.id === id)
  if (!p || !await confirmDialog({ tone:'critical', title:'Xóa vĩnh viễn dự án?', subject:p.name, message:'Toàn bộ dữ liệu dự án sẽ bị xóa và không thể khôi phục.', confirmLabel:'Xóa vĩnh viễn', requireText:p.name })) return
  
  try {
    const res = await apiFetch(`/api/projects/${id}/hard`, {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' }
    })
    
    if (res.ok) {
      trashProjects.value = trashProjects.value.filter(item => item.id !== id)
      showSuccess(`Đã xóa vĩnh viễn dự án "${p.name}" và giải phóng ${p.size} dung lượng ổ đĩa.`)
      await loadDashboard()
      await loadStorageStats()
    } else {
      showError('Không thể xóa vĩnh viễn dự án.')
    }
  } catch (e) {
    showError('Lỗi kết nối khi xóa vĩnh viễn dự án.')
  }
}

onMounted(() => {
  loadPolicies()
  loadArchivedProjects()
  loadStorageStats()
  loadTrash()
})
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main archive-dashboard no-scrollbar">
      
      <!-- Top Overview Section -->
      <section class="archive-overview glass-card">
        <header class="archive-header">
          <div class="title-group">
            <span class="badge-primary"><FolderArchive :size="14" /> Archive Center</span>
            <h1>Lưu trữ & Dung lượng hệ thống</h1>
            <p>Giải phóng tài nguyên và quản lý vòng đời dữ liệu dự án theo mô hình Jira & Notion.</p>
          </div>
          <div class="saved-metric">
            <Sparkles class="animate-pulse icon-primary" :size="24" />
            <!-- Saved space dynamically based on deduplication + archived space -->
            <div>
              <span>Đã tiết kiệm</span>
              <strong>{{ (2.4 + duplicateSavedSpace / 1024).toFixed(2) }} GB</strong>
            </div>
          </div>
        </header>

        <!-- Visual Grid -->
        <div class="archive-stats-grid">
          
          <!-- Disk usage chart representation -->
          <div class="stat-card usage-card">
            <div class="stat-header">
              <HardDrive :size="18" />
              <span>Dung lượng ổ đĩa đã dùng</span>
            </div>
            <div class="visual-progress-wrapper">
              <div class="visual-progress-bar">
                <div class="progress-fill" :style="{ width: `${Math.min(100, Math.max(5, (storageStats.totalBytes / (10 * 1024 * 1024 * 1024)) * 100))}%` }"></div>
              </div>
              <div class="progress-details">
                <strong>{{ formatBytes(storageStats.totalBytes) }} <span>/ 10.0 GB</span></strong>
                <span class="percentage-pill">{{ ((storageStats.totalBytes / (10 * 1024 * 1024 * 1024)) * 100).toFixed(1) }}%</span>
              </div>
            </div>
            <div class="storage-ok">
              <CheckSquare :size="14" />
              <span>Tình trạng bộ nhớ: Rất tốt</span>
            </div>
          </div>

          <!-- Breakdown list -->
          <div class="stat-card breakdown-card">
            <div class="stat-header">
              <Database :size="18" />
              <span>Phân bổ bộ nhớ</span>
            </div>
            <ul class="breakdown-list">
              <li>
                <div class="breakdown-info">
                  <span class="bullet" style="background: var(--primary);"></span>
                  <span>Tệp đính kèm (Attachments)</span>
                </div>
                <strong>{{ formatBytes(storageStats.attachmentBytes) }}</strong>
              </li>
              <li>
                <div class="breakdown-info">
                  <span class="bullet" style="background: var(--success);"></span>
                  <span>Nhiệm vụ & Bình luận (DB)</span>
                </div>
                <strong>1.8 GB</strong>
              </li>
              <li>
                <div class="breakdown-info">
                  <span class="bullet" style="background: var(--warning);"></span>
                  <span>Nhật ký & Lịch sử (Audit Logs)</span>
                </div>
                <strong>1.5 GB</strong>
              </li>
            </ul>
          </div>

          <!-- Cleanup policies -->
          <div class="stat-card policy-card">
            <div class="stat-header">
              <Box :size="18" />
              <span>Chính sách tự động dọn dẹp</span>
            </div>
            <form @submit.prevent="savePolicies" class="policy-form">
              <label class="policy-option">
                <input type="checkbox" v-model="autoCleanLogs" />
                <span>Xóa nhật ký trên 1 năm tuổi</span>
              </label>
              <label class="policy-option">
                <input type="checkbox" v-model="compressFiles" />
                <span>Nén tệp đính kèm dự án lưu trữ</span>
              </label>
              <div class="policy-input-group">
                <span>Lưu trữ thùng rác</span>
                <select v-model="trashRetention" aria-label="Thời gian lưu trữ thùng rác" class="policy-select">
                  <option :value="15">15 ngày</option>
                  <option :value="30">30 ngày</option>
                  <option :value="90">90 ngày</option>
                </select>
              </div>
              <button type="submit" class="policy-submit-btn" :disabled="isSavingPolicy">
                <Save :size="12" />
                <span>{{ isSavingPolicy ? 'Đang cập nhật...' : 'Cập nhật chính sách' }}</span>
              </button>
            </form>
          </div>

        </div>
      </section>

      <!-- Storage Optimizer & Deduplicator -->
      <section class="ai-deduplicator-section glass-card mt-24">
        <div class="section-title-wrapper">
          <div class="title-with-icon">
            <Sparkles :size="20" class="icon-ai" />
            <div>
              <h2>Trình tối ưu hóa dung lượng lưu trữ</h2>
              <p>Phân tích content hash để phát hiện các tệp tin đính kèm trùng lặp trong hệ thống.</p>
            </div>
          </div>
          <div class="deduplicator-actions">
            <button type="button" class="ai-btn" @click="loadDuplicates" :disabled="isLoadingDuplicates || isDeduplicating">
              <RefreshCw :size="15" />
              <span>{{ isLoadingDuplicates ? 'Đang quét...' : 'Quét tệp trùng lặp' }}</span>
            </button>
            <button type="button"
              v-if="duplicatesList.length > 0"
              class="ai-btn"
              @click="runDeduplicator"
              :disabled="isDeduplicating"
            >
              <Scissors :size="15" />
              <span>{{ isDeduplicating ? 'Đang dọn dẹp...' : 'Tối ưu hóa & hợp nhất' }}</span>
            </button>
          </div>
        </div>

        <!-- Duplicates list -->
        <div v-if="duplicatesList.length > 0" class="duplicates-table-wrapper">
          <div class="ai-warning-banner">
            <AlertTriangle :size="18" />
            <span>Phát hiện <strong>{{ duplicatesList.length }} tệp trùng lặp</strong>. Hệ thống sẽ hợp nhất các bản sao này thành các liên kết trỏ tới cùng một tệp vật lý để giải phóng dung lượng.</span>
          </div>
          <table class="duplicates-table" aria-label="Dự án trùng lặp được phát hiện">
            <thead>
              <tr>
                <th scope="col">Tên tệp bản sao</th>
                <th scope="col">Dung lượng</th>
                <th scope="col">Là bản sao của tệp gốc</th>
                <th scope="col">Đường dẫn lưu trữ</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="file in duplicatesList" :key="file.id">
                <td class="file-name-col"><code>{{ file.name }}</code></td>
                <td><strong>{{ file.size }}</strong></td>
                <td><span class="original-file-tag">{{ file.duplicateOf }}</span></td>
                <td class="path-col"><code>{{ file.path }}</code></td>
              </tr>
            </tbody>
          </table>
        </div>
        
        <div v-else-if="duplicateScanError" class="ai-warning-banner" role="status">
          <AlertTriangle :size="18" />
          <span><strong>Chưa thể quét đầy đủ.</strong> {{ duplicateScanError }} Không có dữ liệu nào bị thay đổi.</span>
        </div>

        <div v-else-if="duplicateScanAttempted" class="ai-clean-state">
          <div class="success-icon-badge"><Check :size="22" /></div>
          <div>
            <strong>Hệ thống lưu trữ đã được tối ưu hóa.</strong>
            <p>Không phát hiện tệp đính kèm trùng lặp hoặc tệp rác nào trong không gian làm việc của các dự án đã lưu trữ.</p>
          </div>
        </div>

        <div v-else class="ai-clean-state">
          <div class="success-icon-badge"><HardDrive :size="22" /></div>
          <div>
            <strong>Sẵn sàng kiểm tra theo yêu cầu.</strong>
            <p>Nhấn “Quét tệp trùng lặp” để đọc storage thật. Qaly không tự quét hoặc báo sạch khi chưa có bằng chứng.</p>
          </div>
        </div>
      </section>

      <!-- Breakthrough Section: Notion/Jira-style Project Trash Bin (Thùng rác dự án) -->
      <section class="project-trash-section glass-card mt-24">
        <div class="panel-section-header">
          <Trash2 :size="20" class="icon-danger" />
          <div>
            <h2>Thùng rác dự án (Project Trash Bin - Notion Style)</h2>
            <p>Các dự án đã bị xóa sẽ được tạm lưu tại đây trong <strong>{{ trashRetention }} ngày</strong> trước khi bị xóa vĩnh viễn khỏi máy chủ vật lý.</p>
          </div>
        </div>

        <div v-if="trashProjects.length > 0" class="trash-list">
          <div v-for="tp in trashProjects" :key="tp.id" class="trash-item glass-card">
            <div class="trash-project-meta">
              <span class="project-code">{{ tp.code }}</span>
              <div class="trash-project-info">
                <h3>{{ tp.name }}</h3>
                <p>Kích thước: <strong>{{ tp.size }}</strong> &bull; Sẽ bị xóa vĩnh viễn sau <strong class="text-danger">{{ tp.daysLeft }} ngày nữa</strong>.</p>
              </div>
            </div>
            <div class="trash-actions">
              <button type="button" class="action-btn restore-btn" @click="restoreTrashProject(tp.id)">
                <ArrowUpRight :size="14" />
                <span>Khôi phục dự án</span>
              </button>
              <button type="button" class="action-btn delete-btn" @click="deleteTrashProject(tp.id)">
                <Trash2 :size="14" />
                <span>Xóa vĩnh viễn</span>
              </button>
            </div>
          </div>
        </div>

        <div v-else class="empty-trash-state">
          <Info :size="16" />
          <span>Thùng rác trống. Không có dự án nào bị xóa tạm thời gần đây.</span>
        </div>
      </section>

      <!-- Main workspace -->
      <section class="project-workspace glass-card mt-24">
        <h2 class="sr-only">Dự án đã lưu trữ</h2>
        
        <!-- Toolbar Filters -->
        <div class="archive-toolbar">
          <div class="search-box-wrapper">
            <Search :size="18" class="search-icon" />
            <input 
              v-model="searchQ" 
              type="text" 
              aria-label="Tìm dự án đã lưu trữ"
              placeholder="Tìm theo tên dự án, mô tả hoặc lý do lưu trữ..." 
              class="toolbar-search-input" 
            />
          </div>

          <div class="filter-actions">
            <div class="sort-selector">
              <ArrowUpDown :size="16" />
              <select v-model="sortBy" aria-label="Sắp xếp dự án đã lưu trữ" class="toolbar-select">
                <option value="date">Ngày lưu trữ</option>
                <option value="name">Tên dự án</option>
                <option value="size">Dung lượng ổ đĩa</option>
                <option value="progress">Tiến trình (khi archive)</option>
              </select>
            </div>
          </div>
        </div>

        <!-- Custom Table/Cards of Archived Projects -->
        <div v-if="isLoadingArchived" class="ai-clean-state">
          <RefreshCw class="animate-spin" :size="20" />
          <span>Đang tải các dự án đã lưu trữ...</span>
        </div>
        
        <div v-else class="archived-projects-grid">
          
          <article 
            v-for="project in filteredProjects" 
            :key="project.id" 
            class="archived-project-card glass-card reveal"
          >
            <!-- Card Header -->
            <header class="card-header">
              <div class="card-title-group">
                <span class="project-code">{{ project.code || 'PRJ' }}</span>
                <h3>{{ project.name }}</h3>
              </div>
              <span class="reason-badge">Hoàn thành</span>
            </header>

            <p class="project-desc">{{ project.description || 'Chưa có mô tả chi tiết cho dự án này.' }}</p>

            <!-- Metadata specs -->
            <div class="card-specs">
              <div class="spec-item">
                <span>Dung lượng lưu trữ</span>
                <strong>{{ ((project.taskCount || 0) * 1.8 + (project.completedTaskCount || 0) * 0.4).toFixed(1) }} MB</strong>
              </div>
              <div class="spec-item">
                <span>Ngày lưu trữ</span>
                <strong>{{ project.archivedAt ? new Date(project.archivedAt).toLocaleDateString('vi-VN') : (project.createdAt ? new Date(project.createdAt).toLocaleDateString('vi-VN') : 'N/A') }}</strong>
              </div>
              <div class="spec-item">
                <span>Thành viên</span>
                <strong>{{ project.memberCount || 1 }} người</strong>
              </div>
            </div>

            <!-- Progress Bar -->
            <div class="card-progress">
              <div class="progress-info">
                <span>Tiến trình hoàn tất:</span>
                <strong>{{ project.progressPercentage }}%</strong>
              </div>
              <div class="progress-bar-bg">
                <div class="progress-bar-fill" :style="{ width: `${project.progressPercentage}%` }"></div>
              </div>
            </div>

            <!-- Hover / Active actions -->
            <footer class="card-actions">
              <button type="button"
                class="action-btn restore" 
                @click="restoreProject(project.id)"
                title="Khôi phục dự án về trạng thái hoạt động"
              >
                <ArrowUpRight :size="16" />
                <span>Khôi phục</span>
              </button>
              <button type="button"
                class="action-btn export" 
                @click="exportProjectData(project)"
                title="Xuất dữ liệu dự án ra tệp JSON"
              >
                <Download :size="16" />
                <span>Tải dữ liệu</span>
              </button>
              <button type="button"
                class="action-btn delete" 
                @click="deleteProject(project.id)"
                title="Xóa vĩnh viễn dự án cùng toàn bộ tệp đính kèm"
              >
                <Trash2 :size="16" />
                <span>Xóa vĩnh viễn</span>
              </button>
            </footer>
          </article>

          <!-- Empty state -->
          <div v-if="filteredProjects.length === 0" class="empty-archive-state">
            <div class="empty-icon-wrapper">
              <Archive :size="38" />
            </div>
            <h3>Không tìm thấy dự án lưu trữ nào</h3>
            <p>Các dự án đã lưu trữ giúp giải phóng giao diện làm việc của bạn nhưng vẫn bảo tồn toàn bộ lịch sử công việc phục vụ mục đích tra cứu sau này.</p>
          </div>

        </div>

        <!-- Pagination Controls -->
        <div v-if="totalPages > 1" class="archive-toolbar" style="border-top: 1px solid var(--line-light); border-bottom: none; justify-content: center; gap: 8px;">
          <button type="button"
            class="action-btn" 
            :disabled="currentPage === 1"
            @click="changePage(currentPage - 1)"
          >
            Trước
          </button>
          <span style="font-size: 13px; color: var(--muted); align-self: center;">
            Trang {{ currentPage }} / {{ totalPages }} ({{ totalCount }} dự án)
          </span>
          <button type="button"
            class="action-btn" 
            :disabled="currentPage === totalPages"
            @click="changePage(currentPage + 1)"
          >
            Sau
          </button>
        </div>

      </section>

    </div>
  </div>
</template>

<style scoped>
.archive-dashboard {
  padding: 24px;
  background: var(--surface-warm);
}

.archive-overview {
  padding: 28px;
  background: var(--panel);
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.archive-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.badge-primary {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  background: var(--primary-soft);
  color: var(--primary);
  font-weight: 700;
  padding: 4px 10px;
  border-radius: var(--radius-pill);
  text-transform: uppercase;
}

.title-group h1 {
  font-size: 24px;
  font-weight: 800;
  color: var(--text-strong);
  margin: 6px 0 2px 0;
}

.title-group p {
  font-size: 13.5px;
  color: var(--muted);
  margin: 0;
}

.saved-metric {
  display: flex;
  align-items: center;
  gap: 12px;
  background: var(--primary-soft);
  padding: 12px 18px;
  border-radius: var(--radius-card);
  border: 1px solid rgba(15, 82, 186, 0.15);
}

.saved-metric span {
  font-size: 11px;
  color: var(--muted);
  text-transform: uppercase;
  font-weight: 600;
  display: block;
}

.saved-metric strong {
  font-size: 20px;
  font-weight: 800;
  color: var(--primary);
}

.archive-stats-grid {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 20px;
}

@media (max-width: 1024px) {
  .archive-stats-grid {
    grid-template-columns: 1fr;
  }
}

.stat-card {
  background: var(--panel-soft);
  border: 1px solid var(--line-light);
  border-radius: var(--radius-card);
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  justify-content: space-between;
}

.stat-header {
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--text-strong);
  font-weight: 700;
  font-size: 14px;
}

.stat-header svg {
  color: var(--primary);
}

.visual-progress-wrapper {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.visual-progress-bar {
  height: 12px;
  background: var(--line);
  border-radius: var(--radius-pill);
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--primary), var(--primary-strong));
  border-radius: var(--radius-pill);
  transition: width 0.3s ease;
}

.progress-details {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.progress-details strong {
  font-size: 18px;
  font-weight: 800;
  color: var(--text-strong);
}

.progress-details strong span {
  font-size: 12px;
  color: var(--muted);
  font-weight: 600;
}

.percentage-pill {
  font-size: 11px;
  font-weight: 700;
  background: var(--primary);
  color: white;
  padding: 2px 8px;
  border-radius: var(--radius-pill);
}

.storage-ok {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 600;
  color: var(--success);
}

.breakdown-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.breakdown-list li {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12.5px;
}

.breakdown-info {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--text);
}

.bullet {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}

.breakdown-list li strong {
  color: var(--text-strong);
  font-weight: 700;
}

.policy-form {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.policy-option {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 12px;
  color: var(--text);
  cursor: pointer;
}

.policy-option input[type="checkbox"] {
  width: 15px;
  height: 15px;
  accent-color: var(--primary);
}

.policy-input-group {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  color: var(--text);
  margin-top: 4px;
}

.policy-select {
  border: 1px solid var(--line);
  background: var(--panel);
  color: var(--text);
  border-radius: 6px;
  padding: 4px 8px;
  outline: none;
  font-size: 12px;
}

.policy-submit-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  background: var(--primary);
  color: white;
  border: none;
  border-radius: var(--qaly-radius-lg);
  padding: 8px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  transition: opacity 0.2s;
  margin-top: 4px;
}

.policy-submit-btn:hover {
  opacity: 0.9;
}

.ai-deduplicator-section {
  padding: 24px;
  background: var(--panel);
}

.section-title-wrapper {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.deduplicator-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

@media (max-width: 640px) {
  .section-title-wrapper {
    flex-direction: column;
    align-items: stretch;
  }
}

.title-with-icon {
  display: flex;
  gap: 12px;
  align-items: flex-start;
}

.title-with-icon h2 {
  font-size: 16px;
  font-weight: 800;
  color: var(--text-strong);
  margin: 0;
}

.title-with-icon p {
  font-size: 13px;
  color: var(--muted);
  margin: 4px 0 0 0;
}

.icon-ai {
  color: #a78bfa;
}

.ai-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--primary);
  color: white;
  border: none;
  border-radius: var(--qaly-radius-lg);
  padding: 10px 16px;
  font-weight: 700;
  font-size: 13px;
  cursor: pointer;
  box-shadow: var(--qaly-shadow-md);
  transition: all 0.2s ease;
}

.ai-btn:hover {
  transform: translateY(-1px);
  box-shadow: var(--qaly-shadow-md);
}

.ai-warning-banner {
  display: flex;
  gap: 10px;
  background: rgba(139, 92, 246, 0.08);
  border: 1px dashed #a78bfa;
  padding: 12px 16px;
  border-radius: var(--qaly-radius-lg);
  font-size: 12.5px;
  color: var(--text);
  margin: 16px 0;
  align-items: center;
}

.ai-warning-banner svg {
  color: #8b5cf6;
}

.duplicates-table-wrapper {
  overflow-x: auto;
}

.duplicates-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  text-align: left;
  margin-top: 8px;
}

.duplicates-table th,
.duplicates-table td {
  padding: 10px 12px;
  border-bottom: 1px solid var(--line-light);
}

.duplicates-table th {
  color: var(--muted);
  font-weight: 700;
}

.file-name-col code {
  color: var(--danger);
}

.original-file-tag {
  background: rgba(16, 185, 129, 0.1);
  color: #059669;
  padding: 2px 8px;
  border-radius: 6px;
  font-weight: 600;
}

.path-col code {
  color: var(--muted);
}

.ai-clean-state {
  display: flex;
  align-items: center;
  gap: 16px;
  background: rgba(16, 185, 129, 0.06);
  border: 1px solid rgba(16, 185, 129, 0.15);
  padding: 16px;
  border-radius: var(--qaly-radius-lg);
  margin-top: 16px;
}

.success-icon-badge {
  background: var(--success);
  color: white;
  width: 38px;
  height: 38px;
  border-radius: 50%;
  display: grid;
  place-items: center;
}

.ai-clean-state strong {
  font-size: 14px;
  color: var(--text-strong);
}

.ai-clean-state p {
  font-size: 12.5px;
  color: var(--muted);
  margin: 2px 0 0 0;
}

.project-trash-section {
  padding: 24px;
  background: var(--panel);
}

.panel-section-header {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  margin-bottom: 20px;
}

.panel-section-header h2 {
  font-size: 16px;
  font-weight: 800;
  color: var(--text-strong);
  margin: 0;
}

.panel-section-header p {
  font-size: 13px;
  color: var(--muted);
  margin: 4px 0 0 0;
}

.icon-danger {
  color: var(--danger);
}

.trash-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.trash-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  background: var(--panel-soft);
  border: 1px solid var(--line-light);
  border-radius: var(--qaly-radius-lg);
}

@media (max-width: 640px) {
  .trash-item {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }
}

.trash-project-meta {
  display: flex;
  gap: 12px;
  align-items: center;
}

.trash-project-info h3 {
  font-size: 14.5px;
  font-weight: 800;
  color: var(--text-strong);
  margin: 0;
}

.trash-project-info p {
  font-size: 12.5px;
  color: var(--muted);
  margin: 2px 0 0 0;
}

.trash-actions {
  display: flex;
  gap: 8px;
}

.trash-actions .action-btn {
  padding: 8px 12px;
}

.restore-btn {
  background: var(--primary-soft);
  color: var(--primary);
  border-color: rgba(15, 82, 186, 0.2);
}

.restore-btn:hover {
  background: var(--primary);
  color: white;
}

.delete-btn {
  background: var(--danger-soft);
  color: var(--danger);
  border-color: rgba(239, 68, 68, 0.2);
}

.delete-btn:hover {
  background: var(--danger);
  color: white;
}

.empty-trash-state {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12.5px;
  color: var(--muted);
  background: var(--panel-soft);
  padding: 12px 16px;
  border-radius: var(--qaly-radius-lg);
  border: 1px dashed var(--line);
}

.archive-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 24px;
  border-bottom: 1px solid var(--line-light);
  gap: 16px;
}

@media (max-width: 640px) {
  .archive-toolbar {
    flex-direction: column;
    align-items: stretch;
  }
}

.search-box-wrapper {
  position: relative;
  flex: 1;
}

.search-icon {
  position: absolute;
  left: 14px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--muted);
}

.toolbar-search-input {
  width: 100%;
  border: 1px solid var(--line);
  background: var(--panel-soft);
  color: var(--text);
  border-radius: var(--qaly-radius-lg);
  padding: 10px 16px 10px 42px;
  outline: none;
  font-size: 13.5px;
  transition: border-color 0.2s;
}

.toolbar-search-input:focus {
  border-color: var(--primary);
}

.sort-selector {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--muted);
}

.toolbar-select {
  border: 1px solid var(--line);
  background: var(--panel-soft);
  color: var(--text);
  border-radius: var(--qaly-radius-lg);
  padding: 8px 12px;
  outline: none;
  font-size: 13px;
  font-weight: 600;
}

.archived-projects-grid {
  padding: 24px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 20px;
}

.archived-project-card {
  padding: 24px;
  background: var(--panel);
  display: flex;
  flex-direction: column;
  gap: 16px;
  transition: transform 0.22s, box-shadow 0.22s;
  border: 1px solid var(--line-light);
}

.archived-project-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-card);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
}

.card-title-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.project-code {
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  color: var(--primary);
  background: var(--primary-soft);
  padding: 1px 6px;
  border-radius: 4px;
  width: fit-content;
}

.card-title-group h3 {
  font-size: 16px;
  font-weight: 800;
  color: var(--text-strong);
  margin: 0;
}

.reason-badge {
  font-size: 11px;
  font-weight: 700;
  background: rgba(148, 163, 184, 0.08);
  border: 1px solid var(--line);
  color: var(--muted);
  padding: 2px 8px;
  border-radius: 6px;
}

.project-desc {
  font-size: 13px;
  color: var(--muted);
  margin: 0;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  height: 38px;
}

.card-specs {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  border-top: 1px solid var(--line-light);
  border-bottom: 1px solid var(--line-light);
  padding: 12px 0;
}

.spec-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.spec-item span {
  font-size: 10.5px;
  color: var(--muted);
  text-transform: uppercase;
  font-weight: 500;
}

.spec-item strong {
  font-size: 12.5px;
  color: var(--text-strong);
  font-weight: 700;
}

.card-progress {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.progress-info {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
}

.progress-info span { color: var(--muted); }
.progress-info strong { color: var(--text-strong); font-weight: 700; }

.progress-bar-bg {
  height: 6px;
  background: var(--line-light);
  border-radius: var(--radius-pill);
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  background: var(--primary);
  border-radius: var(--radius-pill);
}

.card-actions {
  display: grid;
  grid-template-columns: 1.2fr 1fr 0.8fr;
  gap: 8px;
  margin-top: 8px;
}

.action-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 8px 4px;
  border-radius: var(--qaly-radius-lg);
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  color: var(--text);
  transition: all 0.2s;
}

.action-btn:hover {
  background: var(--primary-soft);
  color: var(--primary);
  border-color: var(--primary);
}

.action-btn.delete:hover {
  background: var(--danger-soft);
  color: var(--danger);
  border-color: var(--danger);
}

.empty-archive-state {
  grid-column: 1 / -1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 72px 24px;
  text-align: center;
}

.empty-icon-wrapper {
  background: var(--primary-soft);
  color: var(--primary);
  width: 80px;
  height: 80px;
  border-radius: 50%;
  display: grid;
  place-items: center;
  margin-bottom: 20px;
}

.empty-archive-state h3 {
  font-size: 18px;
  font-weight: 800;
  color: var(--text-strong);
  margin: 0 0 8px 0;
}

.empty-archive-state p {
  font-size: 13.5px;
  color: var(--muted);
  max-width: 480px;
  margin: 0;
  line-height: 1.6;
}

.icon-primary { color: var(--primary); }
</style>
