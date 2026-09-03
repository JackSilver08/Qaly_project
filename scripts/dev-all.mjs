import { spawn, spawnSync } from 'node:child_process'
import { createConnection } from 'node:net'
import { existsSync, mkdirSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve(import.meta.dirname, '..')
const includeAi = process.argv.includes('--ai')
const certPath = resolve(root, '.tmp', 'qaly-vite-dev.pfx')
const certPassword = 'qaly-local-dev'
const children = []
let stopping = false
const startupTimeoutMs = Number.parseInt(process.env.QALY_DEV_STARTUP_TIMEOUT_MS ?? '300000', 10)

function localSetting(name, fallback) {
  if (process.env[name]?.trim()) return process.env[name].trim()
  const envPath = resolve(root, '.env')
  if (!existsSync(envPath)) return fallback
  const prefix = `${name}=`
  const line = readFileSync(envPath, 'utf8')
    .split(/\r?\n/)
    .map(value => value.trim())
    .find(value => value.startsWith(prefix))
  if (!line) return fallback
  const value = line.slice(prefix.length).trim()
  return value.replace(/^(['"])(.*)\1$/, '$2') || fallback
}

if (!Number.isFinite(startupTimeoutMs) || startupTimeoutMs < 30000) {
  throw new Error('QALY_DEV_STARTUP_TIMEOUT_MS phải là số nguyên >= 30000.')
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: root, stdio: 'inherit', shell: false, ...options })
  if (result.error || result.status !== 0) throw result.error ?? new Error(`${command} exited with code ${result.status}`)
}

function canRun(command, args) {
  const result = spawnSync(command, args, { cwd: root, stdio: 'ignore', shell: false })
  return !result.error && result.status === 0
}

function portOpen(host, port) {
  return new Promise(resolvePort => {
    const socket = createConnection({ host, port })
    socket.once('connect', () => { socket.destroy(); resolvePort(true) })
    socket.once('error', () => resolvePort(false))
    socket.setTimeout(500, () => { socket.destroy(); resolvePort(false) })
  })
}

async function portInUse(port) {
  const results = await Promise.all([portOpen('127.0.0.1', port), portOpen('::1', port)])
  return results.some(Boolean)
}

async function waitForPort(port, label, child, timeoutMs = startupTimeoutMs) {
  const startedAt = Date.now()
  let lastProgressAt = startedAt
  while (Date.now() - startedAt < timeoutMs) {
    if (await portInUse(port)) return
    if (child.exitCode !== null) {
      throw new Error(`${label} đã dừng với mã ${child.exitCode} trước khi mở cổng ${port}.`)
    }
    if (Date.now() - lastProgressAt >= 30000) {
      const elapsedSeconds = Math.round((Date.now() - startedAt) / 1000)
      console.log(`[Qaly] Vẫn đang chờ ${label} trên cổng ${port} (${elapsedSeconds}s)...`)
      lastProgressAt = Date.now()
    }
    await new Promise(resolveWait => setTimeout(resolveWait, 500))
  }
  throw new Error(`${label} không sẵn sàng trên cổng ${port} sau ${Math.round(timeoutMs / 1000)} giây.`)
}

async function ensureDocker() {
  if (canRun('docker', ['info'])) return
  if (process.platform !== 'win32') throw new Error('Docker chưa chạy. Hãy khởi động Docker rồi chạy lại.')
  const dockerDesktop = 'C:\\Program Files\\Docker\\Docker\\Docker Desktop.exe'
  if (!existsSync(dockerDesktop)) throw new Error('Không tìm thấy Docker Desktop.')
  console.log('[Qaly] Đang khởi động Docker Desktop...')
  spawn(dockerDesktop, [], { detached: true, stdio: 'ignore' }).unref()
  for (let attempt = 0; attempt < 60; attempt++) {
    await new Promise(resolveWait => setTimeout(resolveWait, 2000))
    if (canRun('docker', ['info'])) return
  }
  throw new Error('Docker Desktop chưa sẵn sàng sau 2 phút.')
}

function start(label, command, args, env = {}) {
  const child = spawn(command, args, {
    cwd: root,
    env: { ...process.env, ...env },
    stdio: 'inherit',
    shell: false,
  })
  children.push(child)
  child.once('exit', code => {
    if (!stopping) {
      console.error(`[Qaly] ${label} đã dừng với mã ${code}.`)
      shutdown(code ?? 1)
    }
  })
  return child
}

function openBrowser(url) {
  try {
    if (process.platform === 'win32') {
      spawn('explorer.exe', [url], { detached: true, stdio: 'ignore' }).unref()
    } else if (process.platform === 'darwin') {
      spawn('open', [url], { detached: true, stdio: 'ignore' }).unref()
    } else {
      spawn('xdg-open', [url], { detached: true, stdio: 'ignore' }).unref()
    }
  } catch {
    console.warn(`[Qaly] Không thể tự mở trình duyệt. Hãy mở ${url}`)
  }
}

function shutdown(code = 0) {
  if (stopping) return
  stopping = true
  console.log('\n[Qaly] Đang dừng frontend và backend...')
  for (const child of children) {
    if (!child.pid) continue
    if (process.platform === 'win32') spawnSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore' })
    else child.kill('SIGTERM')
  }
  process.exit(code)
}

async function main() {
  console.log(`[Qaly] Khởi động môi trường phát triển${includeAi ? ' + AI' : ''}...`)
  if (await portInUse(5005)) throw new Error('Cổng 5005 đang được sử dụng. Hãy tắt phiên Qaly cũ trước.')
  if (await portInUse(5173)) throw new Error('Cổng 5173 đang được sử dụng. Hãy tắt phiên Vite cũ trước.')

  await ensureDocker()
  const services = ['qaly-sqlserver', 'qaly-redis', 'qaly-seq', 'qaly-mailhog']
  if (includeAi) services.push('qaly-qdrant', 'qaly-ollama')
  console.log('[Qaly] Kiểm tra hạ tầng Docker...')
  run('docker', ['compose', 'up', '-d', '--wait', ...services])

  if (!existsSync(resolve(root, 'node_modules', 'vite'))) {
    console.log('[Qaly] Cài frontend dependencies lần đầu...')
    run('npm.cmd', ['ci'])
  }
  if (!existsSync(resolve(root, 'src', 'Qaly.Web', 'obj', 'project.assets.json'))) {
    console.log('[Qaly] Restore .NET dependencies lần đầu...')
    run('dotnet', ['restore', 'src/Qaly.Web/Qaly.Web.csproj'])
  }

  mkdirSync(resolve(root, '.tmp'), { recursive: true })
  if (!existsSync(certPath)) {
    console.log('[Qaly] Tạo HTTPS certificate cho Vite (chỉ lần đầu)...')
    run('dotnet', ['dev-certs', 'https', '-ep', certPath, '-p', certPassword])
  }

  console.log('[Qaly] Đang khởi động frontend và backend...')

  const sqlServerPort = localSetting('SQLSERVER_PORT', '1434')
  const sqlServerDatabase = localSetting('SQLSERVER_DATABASE', 'QalyDb')
  const sqlServerPassword = localSetting('SQLSERVER_SA_PASSWORD', 'Qaly@Dev2026!')
  const redisPort = localSetting('REDIS_PORT', '6380')

  const vite = start('Vite', 'node', ['./node_modules/vite/dist/node/cli.js', '--configLoader', 'runner'], {
    QALY_VITE_DEV: '1', QALY_VITE_CERT_PASSWORD: certPassword,
  })
  const backend = start('Backend', 'dotnet', ['watch', '--project', 'src/Qaly.Web', '--no-restore'], {
    DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH: '1',
    ASPNETCORE_URLS: 'https://localhost:5005',
    Vite__DevServerUrl: 'https://localhost:5173',
    // Local SQL Server is bound to localhost only. Keep transport encryption off
    // here because some Windows dev hosts cannot negotiate the container TLS
    // certificate; production connection strings remain unaffected.
    ConnectionStrings__DefaultConnection: `Server=localhost,${sqlServerPort};Database=${sqlServerDatabase};User Id=sa;Password=${sqlServerPassword};Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True`,
    Redis__ConnectionString: `localhost:${redisPort}`,
    // Cloud-backed canonical jobs also need the worker. --ai only controls the
    // optional local Ollama/Qdrant services; it must not leave every AI draft
    // permanently queued in a normal preview.
    AI_JOB_V4_WORKER_ENABLED: 'true',
    PRIVACY_V4_WORKER_ENABLED: 'false',
    GitHub__WorkerEnabled: 'false',
    'Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command': 'Warning',
    'Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Model.Validation': 'Error',
  })

  await Promise.all([
    waitForPort(5173, 'Vite', vite),
    waitForPort(5005, 'Qaly backend', backend),
  ])
  console.log('\n[Qaly] ✓ QALY ĐÃ SẴN SÀNG')
  console.log('[Qaly] Ứng dụng: https://localhost:5005')
  console.log('[Qaly] Frontend HMR: https://localhost:5173')
  console.log('[Qaly] Nhấn Ctrl+C để dừng frontend/backend. Docker được giữ lại cho lần chạy sau.\n')
  openBrowser('https://localhost:5005')
}

process.on('SIGINT', () => shutdown(0))
process.on('SIGTERM', () => shutdown(0))
main().catch(error => { console.error(`[Qaly] ${error.message}`); shutdown(1) })
