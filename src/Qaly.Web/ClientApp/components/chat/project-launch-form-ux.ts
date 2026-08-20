export type LaunchMetricPreset = {
  title: string
  metricType: 'increase' | 'decrease' | 'maintain' | 'delivery'
  unit: string
  dataSource: string
  owner: string
  baselineExample: string
  targetExample: string
}

export type LaunchMetricLike = {
  title: string
  metricType: string
  unit?: string | null
  measurementWindow?: string | null
  dataSource?: string | null
  owner?: string | null
}

export const launchMetricTemplates: LaunchMetricPreset[] = [
  { title: 'Tăng mức độ sử dụng', metricType: 'increase', unit: '%', dataSource: 'Product analytics', owner: 'Product Owner', baselineExample: 'VD: 35', targetExample: 'VD: 55' },
  { title: 'Giảm thời gian xử lý', metricType: 'decrease', unit: 'phút', dataSource: 'Nhật ký hệ thống', owner: 'Process Owner', baselineExample: 'VD: 20', targetExample: 'VD: 10' },
  { title: 'Giảm lỗi nghiệp vụ', metricType: 'decrease', unit: 'lỗi/tháng', dataSource: 'Qaly Tasks / QA', owner: 'QA Lead', baselineExample: 'VD: 12', targetExample: 'VD: 3' },
  { title: 'Đảm bảo SLA/uptime', metricType: 'maintain', unit: '%', dataSource: 'Monitoring', owner: 'Tech Lead', baselineExample: 'VD: 99', targetExample: 'VD: 99.9' },
  { title: 'Tăng mức hài lòng', metricType: 'increase', unit: 'điểm CSAT', dataSource: 'Khảo sát người dùng', owner: 'Product Owner', baselineExample: 'VD: 3.8', targetExample: 'VD: 4.5' },
  { title: 'Hoàn thành đúng hạn', metricType: 'delivery', unit: '%', dataSource: 'Qaly Sprint / Task', owner: 'Project Manager', baselineExample: 'VD: 70', targetExample: 'VD: 90' },
]

function searchable(value: string) {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('vi')
}

export function launchMetricPresetFor(title: string): LaunchMetricPreset {
  const normalized = searchable(title)
  if (normalized.includes('thoi gian') || normalized.includes('toc do') || normalized.includes('latency')) return launchMetricTemplates[1]
  if (normalized.includes('loi') || normalized.includes('defect')) return launchMetricTemplates[2]
  if (normalized.includes('sla') || normalized.includes('uptime') || normalized.includes('san sang')) return launchMetricTemplates[3]
  if (normalized.includes('hai long') || normalized.includes('csat') || normalized.includes('nps')) return launchMetricTemplates[4]
  if (normalized.includes('dung han') || normalized.includes('tien do') || normalized.includes('staffing') || normalized.includes('nghiem thu')) return launchMetricTemplates[5]
  return launchMetricTemplates[0]
}

export function applyLaunchMetricDefaults<T extends LaunchMetricLike>(metric: T, timebox: string): T {
  const preset = launchMetricPresetFor(metric.title)
  return {
    ...metric,
    metricType: !metric.metricType || metric.metricType === 'outcome' ? preset.metricType : metric.metricType,
    unit: metric.unit || preset.unit,
    measurementWindow: metric.measurementWindow || timebox || 'Khi nghiệm thu',
    dataSource: metric.dataSource || preset.dataSource,
    owner: metric.owner || preset.owner,
  }
}

export function launchMetricIntentLabel(metricType: string) {
  if (metricType === 'decrease') return 'Giảm so với hiện tại'
  if (metricType === 'maintain') return 'Duy trì ở mức cam kết'
  if (metricType === 'delivery') return 'Hoàn thành đúng cam kết'
  return 'Tăng so với hiện tại'
}

export function launchMetricValueExample(title: string, field: 'baseline' | 'target') {
  const preset = launchMetricPresetFor(title)
  return field === 'baseline' ? preset.baselineExample : preset.targetExample
}

export function normalizeLaunchTimebox(value?: string | null) {
  const normalized = String(value || '').trim()
  const match = normalized.match(/^(\d+)_weeks?$/i)
  return match ? `${match[1]} tuần` : normalized
}

export function normalizeLaunchAudience(value?: string | null) {
  const normalized = String(value || '').trim()
  const key = normalized.toLocaleLowerCase('vi')
  if (['public', 'public_user', 'public_users'].includes(key)) return 'Người dùng công khai'
  if (['customer', 'customers'].includes(key)) return 'Khách hàng'
  if (['internal', 'admin'].includes(key)) return 'Nội bộ'
  if (['partner', 'partners'].includes(key)) return 'Đối tác'
  return normalized
}

function looksLikeOperationalPrompt(value: string) {
  const normalized = searchable(value)
  return value.length > 260 || [
    'chi hoi toi da', 'cau hoi phai la', 'luu nhieu cau tra loi', 'cho mot xac nhan',
    'read-back', 'provider', 'rulebook',
  ].some(marker => normalized.includes(marker))
}

export function simplifyLaunchObjective(value: string) {
  const original = String(value || '').trim()
  if (!looksLikeOperationalPrompt(original)) return original
  const normalized = searchable(original)
  if (normalized.includes('spa') && (normalized.includes('dich vu') || normalized.includes('booking'))) {
    return 'Ra mắt web SPA để người dùng tìm, đặt và quản lý dịch vụ theo gói trong một luồng rõ ràng.'
  }
  return 'Đưa sản phẩm vào vận hành với phạm vi, trải nghiệm chính và tiêu chí nghiệm thu được xác nhận.'
}

export function simplifyLaunchProblem(value: string, objective: string) {
  const original = String(value || '').trim()
  if (!looksLikeOperationalPrompt(original)) return original
  const normalized = searchable(`${original} ${objective}`)
  if (normalized.includes('spa') && normalized.includes('dich vu')) {
    return 'Quy trình tìm và đặt dịch vụ chưa có một trải nghiệm web thống nhất, dễ theo dõi.'
  }
  return 'Người dùng chưa có một luồng thống nhất để hoàn tất nhu cầu chính của sản phẩm.'
}

export function suggestLaunchBusinessValue(value: string, objective: string) {
  const original = String(value || '').trim()
  if (original && !searchable(original).includes('can nguoi dung xac nhan')) return original
  const normalized = searchable(objective)
  return normalized.includes('dich vu')
    ? 'Giúp khách hàng tự hoàn tất việc đặt dịch vụ và giảm thao tác xử lý thủ công.'
    : 'Giúp người dùng hoàn tất công việc chính nhanh và rõ ràng hơn.'
}
