<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { BrainCircuit, Activity, Target, AlertCircle, PlayCircle, BarChart3, RefreshCw, Server } from 'lucide-vue-next'
import { apiJson, errorMessage } from '../../utils/api-client'
import type { StrategicOverviewDto, AiStrategyResponseDto } from '../../types'

const isLoadingStats = ref(true)
const isLoadingAi = ref(false)

const statsData = ref<StrategicOverviewDto | null>(null)
const aiData = ref<AiStrategyResponseDto | null>(null)
const aiError = ref('')
const canGenerateAiInsight = computed(() => !isLoadingAi.value)

const loadStats = async () => {
  try {
    const res = await apiJson<StrategicOverviewDto>('/api/dashboard/strategic-overview')
    statsData.value = res
  } catch (error) {
    console.error(error)
  } finally {
    isLoadingStats.value = false
  }
}

const generateAiInsight = async () => {
  if (!statsData.value) return

  isLoadingAi.value = true
  aiError.value = ''
  try {
    const res = await apiJson<AiStrategyResponseDto>('/api/dashboard/ai-strategy', {
      method: 'POST',
      body: JSON.stringify(statsData.value)
    })
    aiData.value = res
  } catch (error) {
    aiData.value = null
    aiError.value = errorMessage(error, 'Model AI local chưa sẵn sàng. Hãy kiểm tra Ollama rồi thử lại.')
  } finally {
    isLoadingAi.value = false
  }
}

onMounted(() => {
  loadStats()
})
</script>

<template>
  <section class="strategy-section glass-panel">
    <div class="strategy-header">
      <div class="header-titles">
        <h2>Tổng quan chiến lược</h2>
        <p>AI phân tích dữ liệu dự án, nhiệm vụ và hiệu suất đội nhóm để đề xuất hướng triển khai tiếp theo.</p>
      </div>
      <div class="header-actions">
        <button
          v-if="canGenerateAiInsight"
          @click="generateAiInsight" 
          class="ai-button"
        >
          <RefreshCw v-if="aiData" :size="18" />
          <BrainCircuit v-else :size="18" />
          {{ aiData ? 'Phân tích lại' : 'Tạo phân tích AI' }}
        </button>
      </div>
    </div>

    <div v-if="isLoadingStats" class="strategy-loading">
      Đang tải dữ liệu chiến lược...
    </div>
    
    <div v-else-if="statsData" class="strategy-body">
      
      <!-- Left side: Stats Data -->
      <div class="strategy-stats">
        
        <div class="stat-card">
          <div class="stat-icon"><Activity :size="20"/></div>
          <div class="stat-info">
            <span class="stat-label">Tiến trình chung</span>
            <strong class="stat-value">{{ statsData.averageProjectProgress }}%</strong>
          </div>
        </div>
        
        <div class="stat-card">
          <div class="stat-icon"><Target :size="20"/></div>
          <div class="stat-info">
            <span class="stat-label">Tỷ lệ hoàn thành task</span>
            <strong class="stat-value">{{ statsData.taskCompletionRate }}%</strong>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon alert-icon"><AlertCircle :size="20"/></div>
          <div class="stat-info">
            <span class="stat-label">Dự án rủi ro / Nhiệm vụ trễ</span>
            <strong class="stat-value">{{ statsData.riskProjectCount }} / {{ statsData.overdueTaskCount }}</strong>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon"><BarChart3 :size="20"/></div>
          <div class="stat-info">
            <span class="stat-label">Workload đội nhóm</span>
            <strong class="stat-value">{{ statsData.teamWorkloadLevel }}</strong>
          </div>
        </div>

      </div>

      <!-- Right side: AI Insights -->
      <div class="strategy-ai-insight">
        
        <div v-if="isLoadingAi" class="ai-loading">
          <BrainCircuit class="spin-icon" :size="32" />
          <p>AI đang tổng hợp và phân tích dữ liệu không gian làm việc...</p>
        </div>

        <div v-else-if="aiError" class="ai-error" role="alert">
          <AlertCircle :size="28" />
          <strong>Chưa có kết quả AI thật</strong>
          <p>{{ aiError }}</p>
        </div>

        <div v-else-if="!aiData" class="ai-empty">
          <div class="ai-empty-icon">
            <BrainCircuit :size="48" style="color: rgba(15, 82, 186, 0.3);" />
          </div>
          <p>Nhấn <strong>Tạo phân tích AI</strong> để nhận nhận định chiến lược và đề xuất hành động cho tuần này.</p>
        </div>

        <div v-else class="ai-results">
          <div class="ai-provider-badge">
            <Server :size="14" />
            Kết quả thật từ {{ aiData.provider }} · {{ aiData.model }}
          </div>
          <div class="ai-summary">
            <strong>Nhận định:</strong> {{ aiData.summary }}
          </div>
          
          <div class="ai-grid">
            <div class="ai-box warning-box">
              <h4>Rủi ro chính</h4>
              <ul>
                <li v-for="(risk, i) in aiData.riskAnalysis" :key="i">{{ risk }}</li>
              </ul>
            </div>
            
            <div class="ai-box success-box">
              <h4>Đề xuất hành động</h4>
              <ul>
                <li v-for="(rec, i) in aiData.recommendations" :key="i">{{ rec }}</li>
              </ul>
            </div>
          </div>
          
          <div class="ai-priority">
            <h4><PlayCircle :size="16" style="margin-right: 6px;"/> Ưu tiên xử lý</h4>
            <div class="priority-tags">
              <span v-for="(plan, i) in aiData.priorityPlan" :key="i" class="priority-tag">{{ plan }}</span>
            </div>
          </div>

        </div>
        
      </div>
    </div>
  </section>
</template>

<style scoped>
.glass-panel {
  background: rgba(255, 255, 255, 0.85);
  backdrop-filter: none;
  border: 1px solid rgba(255, 255, 255, 0.6);
  border-radius: var(--qaly-radius-lg);
  padding: 24px;
  box-shadow: var(--qaly-shadow-md);
  margin-top: 24px;
}

.strategy-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
  padding-bottom: 20px;
  border-bottom: 1px solid rgba(15, 82, 186, 0.1);
}

.header-titles h2 {
  font-size: 20px;
  font-weight: 800;
  color: #0f172a;
  margin: 0 0 6px 0;
}

.header-titles p {
  font-size: 14px;
  color: #64748b;
  margin: 0;
  max-width: 500px;
}

.ai-button {
  display: flex;
  align-items: center;
  gap: 8px;
  background: linear-gradient(135deg, #1f80ff, #0f52ba);
  color: white;
  border: none;
  padding: 10px 18px;
  border-radius: var(--qaly-radius-lg);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  transition: transform 0.2s, box-shadow 0.2s;
  box-shadow: var(--qaly-shadow-md);
}

.ai-button:hover {
  transform: translateY(-2px);
  box-shadow: var(--qaly-shadow-md);
}

.secondary-button {
  background: rgba(15, 82, 186, 0.1);
  color: #1f80ff;
  border: none;
  padding: 10px 18px;
  border-radius: var(--qaly-radius-lg);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
}

.strategy-body {
  display: flex;
  gap: 24px;
}

@media (max-width: 900px) {
  .strategy-body {
    flex-direction: column;
  }
}

.strategy-stats {
  flex: 0 0 35%;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 16px;
  background: rgba(255,255,255,0.6);
  padding: 16px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid rgba(15, 82, 186, 0.05);
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: var(--qaly-radius-lg);
  background: rgba(31, 128, 255, 0.1);
  color: #1f80ff;
  display: flex;
  align-items: center;
  justify-content: center;
}

.alert-icon {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.stat-info {
  display: flex;
  flex-direction: column;
}

.stat-label {
  font-size: 12px;
  color: #64748b;
  font-weight: 600;
}

.stat-value {
  font-size: 20px;
  color: #0f172a;
  font-weight: 800;
}

.strategy-ai-insight {
  flex: 1;
  background: linear-gradient(145deg, rgba(31, 128, 255, 0.03), rgba(31, 128, 255, 0.08));
  border-radius: var(--qaly-radius-lg);
  padding: 24px;
  border: 1px solid rgba(31, 128, 255, 0.1);
  display: flex;
  flex-direction: column;
}

.ai-loading, .ai-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  min-height: 200px;
  color: #64748b;
  text-align: center;
}

.ai-error {
  min-height: 200px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  color: #b91c1c;
  text-align: center;
}

.ai-error p {
  max-width: 520px;
  margin: 0;
  color: #64748b;
}

.ai-provider-badge {
  width: fit-content;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 1px solid rgba(5, 150, 105, 0.24);
  border-radius: 6px;
  padding: 6px 9px;
  color: #047857;
  background: rgba(16, 185, 129, 0.08);
  font-size: 12px;
  font-weight: 700;
}

.spin-icon {
  animation: spin 2s linear infinite;
  color: #1f80ff;
  margin-bottom: 16px;
}

@keyframes spin {
  100% { transform: rotate(360deg); }
}

.ai-results {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.ai-summary {
  font-size: 15px;
  line-height: 1.5;
  color: #1e293b;
  background: rgba(255,255,255,0.7);
  padding: 16px;
  border-radius: var(--qaly-radius-lg);
  border-left: 4px solid #1f80ff;
}

.ai-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

@media (max-width: 600px) {
  .ai-grid {
    grid-template-columns: 1fr;
  }
}

.ai-box {
  padding: 16px;
  border-radius: var(--qaly-radius-lg);
  background: rgba(255,255,255,0.7);
}

.ai-box h4 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 700;
}

.ai-box ul {
  margin: 0;
  padding-left: 20px;
  font-size: 13px;
  color: #475569;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.warning-box h4 { color: #ea580c; }
.success-box h4 { color: #059669; }

.ai-priority h4 {
  display: flex;
  align-items: center;
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 700;
  color: #0f172a;
}

.priority-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.priority-tag {
  background: #1f80ff;
  color: white;
  padding: 6px 12px;
  border-radius: var(--qaly-radius-lg);
  font-size: 12px;
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(31, 128, 255, 0.2);
}
</style>
