import test from 'node:test'
import assert from 'node:assert/strict'
import {
  applyLaunchMetricDefaults,
  launchMetricIntentLabel,
  normalizeLaunchAudience,
  normalizeLaunchTimebox,
  simplifyLaunchObjective,
  simplifyLaunchProblem,
  suggestLaunchBusinessValue,
} from '../../src/Qaly.Web/ClientApp/components/chat/project-launch-form-ux.ts'

test('P06 presents stored choices with human labels', () => {
  assert.equal(normalizeLaunchTimebox('12_weeks'), '12 tuần')
  assert.equal(normalizeLaunchAudience('public'), 'Người dùng công khai')
  assert.equal(normalizeLaunchAudience('customer'), 'Khách hàng')
})

test('P06 gives a safe measurement draft without inventing numbers', () => {
  const metric = applyLaunchMetricDefaults({
    title: 'Giảm thời gian xử lý',
    metricType: 'outcome',
    unit: null,
    measurementWindow: null,
    dataSource: null,
    owner: null,
  }, '8 tuần')

  assert.equal(metric.metricType, 'decrease')
  assert.equal(metric.unit, 'phút')
  assert.equal(metric.dataSource, 'Nhật ký hệ thống')
  assert.equal(metric.measurementWindow, '8 tuần')
  assert.equal(launchMetricIntentLabel(metric.metricType), 'Giảm so với hiện tại')
  assert.equal('baseline' in metric, false)
  assert.equal('target' in metric, false)
})

test('P06 turns fallback instructions into friendly goal fields', () => {
  const raw = 'Khởi chạy Project `Spa` cho web SPA đặt dịch vụ. Chỉ hỏi tối đa ba unknown. Câu hỏi phải là form/card, lưu nhiều câu trả lời trước khi gửi và chờ một xác nhận trước khi tạo.'
  const objective = simplifyLaunchObjective(raw)
  assert.equal(objective, 'Ra mắt web SPA để người dùng tìm, đặt và quản lý dịch vụ theo gói trong một luồng rõ ràng.')
  assert.match(simplifyLaunchProblem(raw, objective), /Quy trình tìm và đặt dịch vụ/)
  assert.match(suggestLaunchBusinessValue('Cần người dùng xác nhận giá trị kinh doanh.', objective), /giảm thao tác xử lý thủ công/)
})
