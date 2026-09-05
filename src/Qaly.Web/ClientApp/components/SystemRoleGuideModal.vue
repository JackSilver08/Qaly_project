<script setup lang="ts">
import { computed, onBeforeUnmount, watch } from 'vue'
import { ArrowRight, Bot, CheckCircle2, Compass, Gauge, ShieldCheck, Sparkles, X } from 'lucide-vue-next'

const props = defineProps<{ open: boolean; role: string | null; userName: string; pages: string[] }>()
const emit = defineEmits<{ close: [] }>()

type GuideDetail = { title: string; detail: string }
type GuidePrompt = { label: string; prompt: string }
type RoleGuide = {
  role: 'Admin' | 'Moderator' | 'Member'
  eyebrow: string
  summary: string
  outcomes: Array<{ value: string; label: string }>
  actions: GuideDetail[]
  workflow: GuideDetail[]
  ai: GuideDetail[]
  prompts: GuidePrompt[]
  tone: string
}

const roleGuide = computed<RoleGuide>(() => {
  const normalized = String(props.role || 'Member').trim().toLowerCase()
  if (normalized === 'admin') return {
    role: 'Admin',
    eyebrow: 'Điều hành toàn hệ thống',
    summary: 'biến dữ liệu vận hành thành quyết định có kiểm soát, từ danh mục dự án tới quyền truy cập và năng lực đội ngũ.',
    outcomes: [
      { value: 'Toàn cảnh', label: 'Danh mục & rủi ro' },
      { value: '3 tầng', label: 'RBAC có dấu vết' },
      { value: 'Review trước', label: 'AI không tự ý ghi dữ liệu' },
    ],
    actions: [
      { title: 'Điều hành portfolio', detail: 'Theo dõi danh mục dự án, tiến độ, rủi ro và công suất đội ngũ trên một luồng dữ liệu.' },
      { title: 'Quản trị danh tính & tổ chức', detail: 'Quản lý tài khoản, vai trò hệ thống, tổ chức, thành viên và vai trò nghề nghiệp.' },
      { title: 'Ủy quyền có kiểm soát', detail: 'Cấp phạm vi hỗ trợ có thời hạn cho Moderator.' },
      { title: 'Khởi chạy dự án end-to-end', detail: 'Duyệt brief, đội hình, Sprint, task, dependency và biên nhận đọc lại sau khi tạo.' },
      { title: 'Vận hành dữ liệu demo', detail: 'Quản lý dự án, nhiệm vụ, nhóm, dữ liệu và cấu hình dùng cho demo.' },
    ],
    workflow: [
      { title: '1 · Nhìn toàn cảnh', detail: 'Bắt đầu từ Tổng quan để nhận diện dự án chậm, tải cao và điểm cần chú ý.' },
      { title: '2 · Đi tới bằng chứng', detail: 'Mở Project, Task hoặc hồ sơ thành viên để kiểm tra dữ liệu nguồn.' },
      { title: '3 · Chuẩn bị thay đổi', detail: 'Dùng AI dựng phương án, xem diff và các điều kiện đang chặn.' },
      { title: '4 · Xác nhận & đối chiếu', detail: 'Chỉ ghi khi người có quyền xác nhận; kiểm tra receipt và audit sau thao tác.' },
    ],
    ai: [
      { title: 'Executive summary', detail: 'Tóm tắt portfolio, Sprint, deadline, blocker, rủi ro và xu hướng capacity từ dữ liệu thật.' },
      { title: 'Khởi tạo dự án thông minh', detail: 'Soạn Project Launch Brief, chọn nhân sự theo skill/capacity, chia Sprint và giao task.' },
      { title: 'Hỏi đáp tri thức hệ thống', detail: 'Tóm tắt Wiki, task, tiêu chí nghiệm thu, dependency và lịch sử liên quan thành brief dễ đọc.' },
      { title: 'Kiểm tra quyền & dữ liệu', detail: 'Giải thích role/capability hiệu lực, nguồn dữ liệu và lý do một thao tác bị khóa.' },
      { title: 'Thao tác an toàn', detail: 'Chuẩn bị bản xem trước, nêu tác động, chờ xác nhận và đọc lại dữ liệu sau khi ghi.' },
    ],
    prompts: [
      { label: 'Tình hình toàn hệ thống', prompt: 'Tóm tắt tình hình toàn hệ thống: dự án chậm, task quá hạn, rủi ro và capacity cần chú ý.' },
      { label: 'Ai còn năng lực?', prompt: 'Phân tích ai còn năng lực nhận thêm việc, kèm capacity và bằng chứng kỹ năng.' },
      { label: 'Kiểm tra quyền', prompt: 'Tóm tắt role và capability hiệu lực của tôi, kèm các trang và thao tác tương ứng.' },
    ],
    tone: 'admin',
  }

  if (normalized === 'moderator') return {
    role: 'Moderator',
    eyebrow: 'Hỗ trợ theo phạm vi được giao',
    summary: 'xử lý yêu cầu hỗ trợ đúng tổ chức, capability và thời hạn đang hiệu lực, với mọi bước đều có dấu vết.',
    outcomes: [
      { value: 'Đúng scope', label: 'Tổ chức được giao' },
      { value: 'Có hạn', label: 'Quyền tự hết hiệu lực' },
      { value: 'Audit', label: 'Theo dõi mọi thao tác' },
    ],
    actions: [
      { title: 'Tra cứu đúng phạm vi', detail: 'Xem thành viên của tổ chức thuộc phạm vi hỗ trợ.' },
      { title: 'Hỗ trợ hồ sơ năng lực', detail: 'Hỗ trợ hồ sơ vai trò nghề nghiệp và bằng chứng kỹ năng.' },
      { title: 'Theo dõi ngữ cảnh công việc', detail: 'Theo dõi các dự án và nhiệm vụ mà tài khoản đang tham gia.' },
      { title: 'Xử lý theo capability', detail: 'Thực hiện đúng nhóm hỗ trợ đã được Admin cấp và lưu đầy đủ audit.' },
      { title: 'Điều hướng tới nguồn', detail: 'Mở nhanh hồ sơ, Project hoặc Task cần xử lý thay vì tìm thủ công.' },
    ],
    workflow: [
      { title: '1 · Xác nhận phạm vi', detail: 'Kiểm tra tổ chức, capability và thời hạn assignment đang hiệu lực.' },
      { title: '2 · Đọc dữ liệu nguồn', detail: 'Mở thành viên, vai trò nghề nghiệp, Project và Task liên quan.' },
      { title: '3 · Hỗ trợ có mục tiêu', detail: 'Thực hiện đúng hành động được cấp hoặc hướng dẫn người có quyền.' },
      { title: '4 · Bàn giao rõ ràng', detail: 'Tóm tắt kết quả, nguồn đã kiểm tra và bước tiếp theo.' },
    ],
    ai: [
      { title: 'Tóm tắt case hỗ trợ', detail: 'Gom tài khoản, tổ chức, dự án và hoạt động liên quan trong đúng scope.' },
      { title: 'Tóm tắt tri thức', detail: 'Rút gọn Wiki, yêu cầu, quy trình và bằng chứng thành nội dung dễ bàn giao.' },
      { title: 'Giải thích dữ liệu', detail: 'Nêu nguồn, trạng thái, role/capability và nguyên nhân của tình huống hiện tại.' },
      { title: 'Gợi ý bước tiếp theo', detail: 'Đề xuất quy trình xử lý và mở đúng trang, hồ sơ hoặc control cần thao tác.' },
    ],
    prompts: [
      { label: 'Tóm tắt case', prompt: 'Tóm tắt case hỗ trợ hiện tại trong phạm vi của tôi, kèm dữ liệu nguồn và bước tiếp theo.' },
      { label: 'Kiểm tra phạm vi', prompt: 'Cho tôi biết phạm vi Moderator đang hiệu lực và những capability có thể dùng.' },
      { label: 'Tìm dữ liệu liên quan', prompt: 'Tìm và tóm tắt các dữ liệu liên quan tới ngữ cảnh đang mở trong phạm vi hỗ trợ.' },
    ],
    tone: 'moderator',
  }

  return {
    role: 'Member',
    eyebrow: 'Thực hiện công việc được giao',
    summary: 'tập trung vào việc quan trọng, nắm đủ ngữ cảnh và hoàn tất quy trình minh chứng — duyệt mà không phải dò nhiều màn hình.',
    outcomes: [
      { value: 'Một nơi', label: 'Việc & deadline' },
      { value: 'Rõ DoD', label: 'Đúng tiêu chí' },
      { value: 'Có evidence', label: 'Duyệt minh bạch' },
    ],
    actions: [
      { title: 'Nắm đúng việc của mình', detail: 'Xem dự án, Sprint và nhiệm vụ có liên quan tới mình.' },
      { title: 'Thực thi có ngữ cảnh', detail: 'Đọc yêu cầu, dependency, tiêu chí nghiệm thu và Definition of Done.' },
      { title: 'Ghi nhận tiến độ', detail: 'Cập nhật tiến độ, ghi giờ, trao đổi và đính kèm tài liệu.' },
      { title: 'Gửi duyệt đúng quy trình', detail: 'Gửi nhiệm vụ sang duyệt kèm minh chứng nghiệm thu.' },
      { title: 'Theo dõi tới khi đóng', detail: 'Theo dõi phản hồi, blocker và lịch sử hoạt động của công việc.' },
    ],
    workflow: [
      { title: '1 · Chọn việc ưu tiên', detail: 'Mở Nhiệm vụ để xem deadline, trạng thái và blocker của chính bạn.' },
      { title: '2 · Hiểu trước khi làm', detail: 'Đọc mô tả, acceptance criteria, dependency hoặc nhờ AI tóm tắt.' },
      { title: '3 · Cập nhật bằng chứng', detail: 'Ghi giờ, trao đổi, tải tài liệu và đánh dấu nội dung minh chứng.' },
      { title: '4 · Gửi reviewer', detail: 'Chuyển sang Đang duyệt và xử lý phản hồi tới khi được chấp nhận.' },
    ],
    ai: [
      { title: 'Daily brief cá nhân', detail: 'Tóm tắt việc cần làm, deadline, mức ưu tiên và blocker của bạn.' },
      { title: 'Tóm tắt kiến thức', detail: 'Gom Wiki, mô tả Task, trao đổi và tài liệu liên quan thành brief ngắn để bắt đầu nhanh.' },
      { title: 'Giải thích yêu cầu', detail: 'Diễn giải tiêu chí nghiệm thu, Definition of Done, dependency và thứ tự thực hiện.' },
      { title: 'Chuẩn bị nội dung', detail: 'Soạn checklist, subtask, cập nhật tiến độ và nội dung bàn giao để bạn duyệt.' },
      { title: 'Điều hướng công việc', detail: 'Tìm đúng Task, Project hoặc dữ liệu bạn được xem và dẫn tới màn hình liên quan.' },
    ],
    prompts: [
      { label: 'Ưu tiên hôm nay', prompt: 'Tóm tắt các task tôi cần ưu tiên hôm nay, deadline và blocker.' },
      { label: 'Tóm tắt kiến thức', prompt: 'Tóm tắt kiến thức, yêu cầu, tiêu chí nghiệm thu và dependency liên quan tới task đang mở.' },
      { label: 'Chuẩn bị gửi duyệt', prompt: 'Kiểm tra task đang mở và soạn checklist những gì tôi cần hoàn tất trước khi gửi duyệt.' },
    ],
    tone: 'member',
  }
})

const visiblePages = computed(() => [...new Set(props.pages.filter(Boolean))])
function close() { emit('close') }
function openAssistant(prompt: string) {
  window.dispatchEvent(new CustomEvent('qaly:open-ai-assistant', { detail: { prompt } }))
  close()
}
function handleKeydown(event: KeyboardEvent) { if (event.key === 'Escape' && props.open) close() }

watch(() => props.open, (open) => {
  if (typeof document !== 'undefined') document.body.classList.toggle('role-guide-open', open)
}, { immediate: true })
if (typeof window !== 'undefined') window.addEventListener('keydown', handleKeydown)
onBeforeUnmount(() => {
  if (typeof window !== 'undefined') window.removeEventListener('keydown', handleKeydown)
  if (typeof document !== 'undefined') document.body.classList.remove('role-guide-open')
})
</script>

<template>
  <Teleport to="body">
    <Transition name="role-guide">
      <div v-if="open" class="role-guide-backdrop" @click.self="close">
        <section class="role-guide-modal" :class="`role-guide-modal--${roleGuide.tone}`" role="dialog" aria-modal="true" :aria-label="`Hướng dẫn sử dụng cho ${roleGuide.role}`" data-testid="system-role-guide">
          <header class="role-guide-header">
            <div class="role-guide-heading">
              <div class="role-guide-symbol"><ShieldCheck :size="31" /></div>
              <div>
                <span>{{ roleGuide.eyebrow }}</span>
                <h2>{{ roleGuide.role }} · Bản đồ làm việc hiệu quả</h2>
                <p class="role-guide-summary"><strong>{{ userName }}</strong><span>{{ roleGuide.summary }}</span></p>
              </div>
            </div>
            <div class="role-guide-outcomes" aria-label="Giá trị nổi bật">
              <span v-for="item in roleGuide.outcomes" :key="item.label"><strong>{{ item.value }}</strong><small>{{ item.label }}</small></span>
            </div>
            <button type="button" aria-label="Đóng hướng dẫn" @click="close"><X :size="21" /></button>
          </header>

          <div class="role-guide-content">
            <div class="role-guide-main-grid">
              <div class="role-guide-core-column">
                <article class="role-guide-actions-card">
                  <div class="role-guide-card-title"><CheckCircle2 :size="20" /><strong>Bạn có thể làm</strong></div>
                  <ul class="role-guide-details"><li v-for="item in roleGuide.actions" :key="item.title"><strong>{{ item.title }}</strong><span>{{ item.detail }}</span></li></ul>
                </article>
                <article class="role-guide-pages-card">
                  <div class="role-guide-card-title role-guide-pages-title">
                    <Compass :size="20" />
                    <span><strong>Điểm đến theo quyền</strong><small>Đi theo số thứ tự để khám phá đúng luồng của role hiện tại.</small></span>
                  </div>
                  <nav class="role-guide-pages" aria-label="Các trang role hiện tại có thể truy cập">
                    <span v-for="(page, index) in visiblePages" :key="page" :title="page"><b>{{ index + 1 }}</b>{{ page }}</span>
                  </nav>
                </article>
              </div>
              <article class="role-guide-ai-card">
                <div class="role-guide-card-title"><Bot :size="21" /><strong>Trợ lý AI hỗ trợ</strong></div>
                <ul class="role-guide-details role-guide-ai-details"><li v-for="item in roleGuide.ai" :key="item.title"><strong>{{ item.title }}</strong><span>{{ item.detail }}</span></li></ul>
                <div class="role-guide-prompts">
                  <small><Sparkles :size="14" /> Prompt dùng ngay</small>
                  <button v-for="item in roleGuide.prompts" :key="item.label" type="button" @click="openAssistant(item.prompt)">{{ item.label }} <ArrowRight :size="13" /></button>
                </div>
              </article>
            </div>
            <section class="role-guide-workflow">
              <div class="role-guide-workflow-heading">
                <div class="role-guide-card-title"><Gauge :size="20" /><strong>Luồng làm việc hiệu suất cao</strong></div>
                <span>Quan sát <ArrowRight :size="13" /> kiểm chứng <ArrowRight :size="13" /> chuẩn bị <ArrowRight :size="13" /> xác nhận</span>
              </div>
              <ol>
                <li v-for="(item, index) in roleGuide.workflow" :key="item.title">
                  <b class="role-guide-step">{{ index + 1 }}</b>
                  <div><strong>{{ item.title.replace(/^\d+\s*·\s*/, '') }}</strong><span>{{ item.detail }}</span></div>
                  <ArrowRight v-if="index < roleGuide.workflow.length - 1" class="role-guide-step-arrow" :size="18" />
                </li>
              </ol>
            </section>
          </div>

          <footer>
            <span><Sparkles :size="14" /> AI luôn giữ đúng dữ liệu và quyền của ngữ cảnh hiện tại. Chọn tên/vai trò ở thanh bên để mở lại.</span>
            <button type="button" @click="close">Bắt đầu làm việc</button>
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
:global(body.role-guide-open) { overflow: hidden; }
.role-guide-backdrop { position: fixed; inset: 0; z-index: 1600; display: grid; place-items: center; padding: 20px; background: rgba(15,23,42,.62); backdrop-filter: blur(7px); }
.role-guide-modal { --guide-accent:#2563eb; --guide-accent-soft:#eff6ff; width:min(1220px,100%); max-height:min(880px,calc(100vh - 40px)); overflow:auto; border:1px solid rgba(148,163,184,.32); border-radius:26px; background:#fff; box-shadow:0 34px 90px rgba(15,23,42,.34); }
.role-guide-modal--moderator { --guide-accent:#0f766e; --guide-accent-soft:#ecfdf5; }
.role-guide-modal--member { --guide-accent:#4f46e5; --guide-accent-soft:#eef2ff; }
.role-guide-header { position:relative; display:grid; grid-template-columns:minmax(0,1fr) auto; gap:18px 28px; align-items:center; padding:28px 32px 25px; overflow:hidden; color:#fff; background:linear-gradient(130deg,var(--guide-accent),#172554 76%); }
.role-guide-header::after { content:''; position:absolute; right:-90px; bottom:-135px; width:330px; height:330px; border:1px solid rgba(255,255,255,.15); border-radius:50%; box-shadow:0 0 0 54px rgba(255,255,255,.035),0 0 0 108px rgba(255,255,255,.025); }
.role-guide-heading { display:flex; gap:16px; align-items:flex-start; min-width:0; }
.role-guide-symbol { display:grid; place-items:center; flex:0 0 auto; width:56px; height:56px; border-radius:17px; background:rgba(255,255,255,.16); }
.role-guide-header span { font-size:12px; font-weight:900; letter-spacing:.1em; text-transform:uppercase; opacity:.86; }
.role-guide-header h2 { margin:4px 0 6px; font-size:clamp(25px,3vw,34px); line-height:1.15; }
.role-guide-summary { display:flex; gap:8px; align-items:flex-start; width:fit-content; margin:10px 0 0!important; padding:9px 12px; border-left:3px solid rgba(255,255,255,.9); border-radius:0 10px 10px 0; color:rgba(255,255,255,.94)!important; background:rgba(3,10,40,.2); font-size:13px; line-height:1.5!important; opacity:1!important; text-shadow:0 1px 2px rgba(3,10,40,.28); }
.role-guide-summary strong { flex:0 0 auto; color:#fff!important; }
.role-guide-summary span { max-width:650px; color:rgba(255,255,255,.94)!important; font-size:inherit!important; font-weight:600!important; letter-spacing:0!important; text-transform:none!important; opacity:1!important; }
.role-guide-header > button { position:absolute; z-index:2; top:22px; right:24px; display:grid; place-items:center; width:40px; height:40px; border:1px solid rgba(255,255,255,.3); border-radius:12px; color:#fff; background:rgba(255,255,255,.1); cursor:pointer; transition:transform .18s ease,background .18s ease; }
.role-guide-header > button:hover { transform:rotate(5deg) scale(1.05); background:rgba(255,255,255,.2); }
.role-guide-outcomes { position:relative; z-index:1; display:flex; gap:8px; padding-right:48px; }
.role-guide-outcomes > span { display:grid; min-width:104px; padding:10px 12px; border:1px solid rgba(255,255,255,.2); border-radius:13px; background:rgba(255,255,255,.09); backdrop-filter:blur(5px); }
.role-guide-outcomes strong { font-size:14px; }
.role-guide-outcomes small { margin-top:2px; font-size:10px; opacity:.76; }
.role-guide-content { padding:20px 24px 17px; }
.role-guide-main-grid { display:grid; grid-template-columns:minmax(0,1.25fr) minmax(370px,.75fr); gap:14px; align-items:stretch; }
.role-guide-core-column { display:grid; grid-template-rows:auto auto; gap:14px; min-width:0; }
.role-guide-main-grid article,.role-guide-workflow { min-width:0; padding:17px 18px; border:1px solid #dbe4f0; border-radius:17px; background:#f8fafc; }
.role-guide-ai-card { grid-column:2; grid-row:1; background:linear-gradient(145deg,var(--guide-accent-soft),#f8fafc)!important; box-shadow:inset 3px 0 0 color-mix(in srgb,var(--guide-accent) 72%,white); }
.role-guide-pages-card { padding:15px 17px!important; background:#fff!important; }
.role-guide-card-title { display:flex; align-items:center; gap:9px; color:var(--guide-accent); }
.role-guide-card-title strong { color:#172033; font-size:15px; }
.role-guide-actions-card .role-guide-details { grid-template-columns:repeat(2,minmax(0,1fr)); }
.role-guide-details { display:grid; gap:10px; margin:14px 0 0; padding:0; list-style:none; }
.role-guide-details li { position:relative; display:grid; gap:2px; padding-left:18px; color:#42526a; font-size:12px; line-height:1.4; }
.role-guide-details li::before { content:'✓'; position:absolute; left:0; top:1px; color:var(--guide-accent); font-weight:900; }
.role-guide-details li strong { color:#25324a; font-size:12px; }
.role-guide-ai-details { grid-template-columns:1fr; gap:11px; }
.role-guide-pages-title { align-items:flex-start; }
.role-guide-pages-title > span { display:grid; gap:2px; }
.role-guide-pages-title small { color:#64748b; font-size:10px; font-weight:600; line-height:1.35; }
.role-guide-pages { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:7px; margin-top:12px; }
.role-guide-pages span { display:flex; min-width:0; gap:7px; align-items:center; padding:7px 9px; border:1px solid #cbdcf7; border-radius:10px; color:#1e3a5f; background:#f8fbff; font-size:10px; font-weight:800; line-height:1.25; }
.role-guide-pages b { display:grid; place-items:center; flex:0 0 20px; width:20px; height:20px; border-radius:7px; color:#fff; background:var(--guide-accent); font-size:9px; }
.role-guide-prompts { display:flex; flex-wrap:wrap; gap:7px; margin-top:14px; padding-top:12px; border-top:1px solid rgba(148,163,184,.28); }
.role-guide-prompts small { display:flex; flex:0 0 100%; gap:6px; align-items:center; color:#596983; font-weight:750; }
.role-guide-prompts button { display:inline-flex; gap:5px; align-items:center; padding:6px 9px; border:1px solid color-mix(in srgb,var(--guide-accent) 28%,white); border-radius:999px; color:var(--guide-accent); background:#fff; font-size:10px; font-weight:800; cursor:pointer; transition:transform .16s ease,box-shadow .16s ease; }
.role-guide-prompts button:hover { transform:translateY(-1px); box-shadow:0 5px 14px rgba(37,99,235,.12); }
.role-guide-workflow { margin-top:14px; background:#fff; }
.role-guide-workflow-heading { display:flex; justify-content:space-between; gap:16px; align-items:center; }
.role-guide-workflow-heading > span { display:inline-flex; gap:5px; align-items:center; color:#64748b; font-size:10px; font-weight:800; text-transform:uppercase; }
.role-guide-workflow ol { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:9px; margin:12px 0 0; padding:0; list-style:none; }
.role-guide-workflow li { position:relative; display:flex; gap:9px; align-items:flex-start; min-height:72px; padding:11px 25px 11px 10px; border:1px solid #dbe4f0; border-radius:12px; background:linear-gradient(145deg,#fff,#f5f8fd); }
.role-guide-workflow li > div { display:grid; gap:4px; }
.role-guide-workflow li strong { color:#25324a; font-size:11px; }
.role-guide-workflow li span { color:#64748b; font-size:10px; line-height:1.4; }
.role-guide-step { display:grid; place-items:center; flex:0 0 25px; width:25px; height:25px; border-radius:9px; color:#fff; background:var(--guide-accent); font-size:11px; box-shadow:0 5px 12px color-mix(in srgb,var(--guide-accent) 24%,transparent); }
.role-guide-step-arrow { position:absolute; z-index:2; right:-14px; top:27px; padding:3px; border:1px solid #cbdcf7; border-radius:50%; color:var(--guide-accent); background:#fff; box-sizing:content-box; }
.role-guide-modal footer { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:0 24px 21px; color:#64748b; font-size:12px; }
.role-guide-modal footer > span { display:inline-flex; gap:7px; align-items:center; }
.role-guide-modal footer button { flex:0 0 auto; border:0; border-radius:11px; padding:11px 18px; color:#fff; background:var(--guide-accent); font-weight:850; cursor:pointer; transition:transform .16s ease,box-shadow .16s ease; }
.role-guide-modal footer button:hover { transform:translateY(-1px); box-shadow:0 8px 20px color-mix(in srgb,var(--guide-accent) 28%,transparent); }
.role-guide-enter-active,.role-guide-leave-active { transition:opacity .24s ease; }
.role-guide-enter-active .role-guide-modal,.role-guide-leave-active .role-guide-modal { transition:transform .32s cubic-bezier(.2,.9,.25,1),opacity .24s ease; }
.role-guide-enter-from,.role-guide-leave-to { opacity:0; }
.role-guide-enter-from .role-guide-modal { opacity:0; transform:translateY(22px) scale(.965); }
.role-guide-leave-to .role-guide-modal { opacity:0; transform:translateY(10px) scale(.98); }
.role-guide-enter-active .role-guide-main-grid > article,.role-guide-enter-active .role-guide-workflow { animation:guide-card-in .38s ease both; }
.role-guide-enter-active .role-guide-main-grid > article:nth-child(2) { animation-delay:.05s; }
.role-guide-enter-active .role-guide-main-grid > article:nth-child(3) { animation-delay:.1s; }
.role-guide-enter-active .role-guide-workflow { animation-delay:.14s; }
@keyframes guide-card-in { from { opacity:0; transform:translateY(9px); } to { opacity:1; transform:translateY(0); } }
@media (max-width:1000px) { .role-guide-header { grid-template-columns:1fr; } .role-guide-outcomes { padding-right:0; } .role-guide-main-grid { grid-template-columns:minmax(0,1fr) minmax(310px,.8fr); } .role-guide-pages { grid-template-columns:repeat(2,minmax(0,1fr)); } }
@media (max-width:760px) { .role-guide-backdrop { padding:10px; } .role-guide-header { padding:22px 20px; } .role-guide-heading { padding-right:34px; } .role-guide-symbol { display:none; } .role-guide-summary { display:grid; } .role-guide-outcomes { overflow-x:auto; } .role-guide-outcomes > span { min-width:98px; } .role-guide-content { padding:14px; } .role-guide-main-grid { grid-template-columns:1fr; } .role-guide-ai-card { grid-column:1; grid-row:2; } .role-guide-actions-card .role-guide-details { grid-template-columns:1fr; } .role-guide-pages { grid-template-columns:repeat(2,minmax(0,1fr)); } .role-guide-workflow-heading { align-items:flex-start; flex-direction:column; } .role-guide-workflow ol { grid-template-columns:1fr; } .role-guide-step-arrow { right:auto; left:14px; top:auto; bottom:-15px; transform:rotate(90deg); } .role-guide-modal footer { align-items:stretch; flex-direction:column; padding:0 14px 17px; } }
@media (prefers-reduced-motion:reduce) { .role-guide-enter-active,.role-guide-leave-active,.role-guide-enter-active .role-guide-modal,.role-guide-leave-active .role-guide-modal { transition:none; } .role-guide-enter-active .role-guide-main-grid > article,.role-guide-enter-active .role-guide-workflow { animation:none; } }
</style>
