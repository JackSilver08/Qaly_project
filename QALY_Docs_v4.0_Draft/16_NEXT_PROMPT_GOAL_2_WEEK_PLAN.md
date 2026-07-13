# Prompt: Consolidate Qaly v4.0 Goal and Two-Week Team Plan

Ban tiep tuc dong vai Principal Product Architect, Senior Business Analyst,
Staff Software Engineer va QA/DevSecOps Lead cho du an Qaly.

## Muc tieu cua lan chay nay

Tong hop toan bo gap cua cac chuc nang da lam tu P0-00 den P0-03, cac gap
nghiep vu va cac muc con thieu so voi QALY_Docs_v4.0_Draft va Goal dai han.
Sau do tao mot file Goal/Execution Plan du chi tiet de chia viec cho 5 thanh
vien trong 2 dot, tong thoi gian 2 tuan:

- Trung
- Khang
- Duy Hoang
- Minh
- Long

Day la nhiem vu audit va lap ke hoach. KHONG sua source, migration, test,
workflow hay cac tai lieu v4 hien co trong lan chay nay.

## Dinh nghia bat buoc

Trong audit nay, khong bat buoc dung du lieu production hay du lieu nguoi dung
that. Seed, fixture, fake provider va synthetic test data duoc chap nhan neu
duoc ghi ro va luong co the chay lap lai.

Mot chuc nang duoc xem la "hoat dong that" khi:

1. Nguoi dung hoac automated test co the kich hoat luong.
2. UI co handler that va goi dung API/service, hoac backend co entry point co
   the chay duoc.
3. He thong co state transition/ket qua co the quan sat va kiem chung.
4. Khong hard-code thong bao thanh cong de che request that bai.
5. Khong loi im lang; loading, error va degraded state phai phan anh dung.
6. Luong co test hoac cach runtime verification lap lai duoc.

"Ao" co nghia la UI tinh, stub khong co implementation, success hard-code,
seed bi trinh bay nhu ket qua vua xu ly, endpoint khong duoc goi, hoac luong
khong the lap lai. Seed/demo data tu no KHONG bi coi la ao neu duoc gan nhan
dung va duoc dung de kiem thu mot implementation co that.

Phan biet rieng:

- Product bug: code/luong sai.
- Integration gap: cac module co that nhung chua noi duoc voi nhau.
- Environment gap: Redis, LiveKit, provider hoac worker chua cau hinh.
- Verification gap: co implementation nhung chua co test/runtime evidence.
- Documentation gap: Doc v4 khong khop source/runtime.
- UX/QoL gap: luong chay duoc nhung nguoi dung kho tim, kho quay lai hoac phai
  nhap lai thong tin.

## Cam ket bao phu 100% va y nghia chinh xac

"Bao phu 100%" trong lan audit nay co nghia la 100% item trong tap kiem ke
huu han da duoc doc, gan ID, doi chieu va co mot disposition ro rang. No khong
co nghia la tuy tien khang dinh 100% source da hoan thanh, hoac ep tat ca gap
vao hai tuan.

Phai tao hai inventory doc lap roi reconciliation hai chieu:

### A. Requirement Universe

Liet ke tung dong co the nghiem thu tu Goal, ProductHub, scope/baseline v3.2,
toan bo Doc v4, ADR, AI contract, UX map, acceptance checklist, risk register,
roadmap va evidence P0-00 den P0-03. Moi item phai co `REQ-ID` duy nhat va mot
trang thai. Khong duoc chi doc cac file "dac biet" roi bo qua file con lai.

### B. Implementation Surface Universe

Kiem ke toan bo be mat co the chua implementation hoac gap:

- UI route/page/component/form/action/navigation va state loading/error/empty.
- Controller/API endpoint/request/response/authorization/validation.
- Application service, domain rule, entity, state machine va background job.
- Database schema, constraint, index, migration, seed va data cleanup.
- Queue/cache/provider/external adapter/config/feature flag/health check.
- Unit/integration/contract/E2E/security/performance/recovery test.
- CI workflow, deploy config, generated bundle, runbook, observability va audit.

Moi implementation surface phai co `IMP-ID`, owner module va mapping ve it
nhat mot REQ-ID, hoac duoc gan `Orphan implementation`/`Out-of-scope` kem ly do.

### C. Closure Gate

Bao cao chi dat coverage gate khi:

1. `Requirements inventoried / total requirements = 100%`.
2. `Implementation surfaces inventoried / total discovered surfaces = 100%`.
3. Moi REQ-ID co status, evidence va it nhat mot GAP-ID hoac VERIFIED-ID.
4. Moi IMP-ID co REQ-ID hoac disposition ro rang.
5. So requirement khong mapping = 0.
6. So implementation khong disposition = 0.
7. So gap khong co owner/sprint/backlog disposition = 0.
8. So acceptance item khong co verification method = 0.
9. So task cam ket khong map ve GAP-ID va REQ-ID = 0.

Neu bat ky mau so nao chua xac dinh, khong duoc ghi 100%. Phai ghi
`Coverage gate FAILED`, liet ke dung item chua kiem ke va tiep tuc audit truoc
khi chia viec. Khong duoc dung "cac file quan trong", sampling hoac suy luan
de thay cho full inventory.

Khong danh dong environment gap thanh product bug. Tuy nhien, moi dependency
ngoai phai co mot cach kiem thu local/CI bang test double, fake provider hoac
degraded mode trung thuc; khong duoc gia lap success production.

## Nguon phai doc va doi chieu

1. Goal dai han hien tai cua task/thread neu cong cu Goal con truy cap duoc.
2. Toan bo `QALY_Docs_v4.0_Draft/`, dac biet:
   - `01_Executive_Audit_and_ChangeLog.md`
   - `02_Product_Vision_and_Scope_Lock.md`
   - `04_Traceability_Matrix_v4.0.csv`
   - `05_Architecture_and_ADR/`
   - `06_AI_Native_and_Endpoint_Contract_v4.0.md`
   - `08_UX_Entity_Link_and_QoL_Map.md`
   - `10_Acceptance_Checklist_v4.0.csv`
   - `11_Risk_Register_v4.0.csv`
   - `12_Roadmap_and_Migration_Plan.md`
   - `13_P0-01_CI_Security_Evidence.md`
   - `14_P0-02_Canonical_AI_Job_Evidence.md`
   - `15_P0-03_Privacy_Retention_DSAR_Evidence.md`
3. Source, migrations, tests, CI/CD, appsettings va runtime preview hien tai.
4. `ProjectHub.docx` va baseline v3.2 chi khi can xac minh scope goc ma v4
   chua noi ro.
5. Git history, git blame va commit/file ownership cua tung thanh vien.
6. Bao cao audit/runtime truoc do chi la dau moi; phai xac minh lai bang code,
   test, API, database hoac runtime.

## Ket qua runtime da biet, can kiem chung lai

Khong mac dinh cac ket luan sau la dung vinh vien. Hay tai hien nhanh va cap
nhat evidence:

- Project va Task co du lieu va API; mo Task tu Project chua cap nhat URL
  canonical, du URL truc tiep co the khoi phuc drawer.
- Project co `SourceGroupId` trong database, nhung Project va Group chua co
  dieu huong hai chieu ro rang tren UI.
- Group tao duoc Meeting session va `meetingId`; LiveKit chua cau hinh nen
  media/transcript truc tiep chua chay.
- Meeting AI chan dung khi transcript trong; AI worker hien tat/degraded nen
  chua tai hien duoc Meeting -> AI result -> Task draft tu mot phien moi.
- AI Activity doc job tu backend va hien `worker_disabled`; job thanh cong va
  task draft co san co the la seed/demo, khong phai ket qua cua phien vua tao.
- Privacy API da duoc thu tao policy, ghi database va disable thanh cong.
- Nut UI Settings -> Privacy -> Retention -> Tao policy da bam nhung khong tao
  record, can truy nguyen request/error/toast/CSRF va them regression test.
- Redis chua san sang lam global health tra 503; can xac dinh day la expected
  degraded mode hay blocker theo acceptance cua tung moi truong.

## Audit Git ownership truoc khi chia viec

Chay `git shortlog`, `git log --author`, `git log --name-only`, `git blame` va
doc cac commit lien quan cua tung nguoi. Lap bang:

`Thanh vien -> Git alias/email -> module/file da lam -> diem manh -> vung nen
tranh -> evidence commit`.

Alias da nhin thay nhung van phai xac minh:

- Trung co the lien quan `jackhanma123 <trungnguyendoan9@gmail.com>`.
- Khang co the lien quan `khangphma2511 <khangplctb01620@gmail.com>`.
- Duy Hoang co the lien quan `duyh-dev`.
- Long co the lien quan `GiaLong281 <longngtb01597@gmail.com>`.
- Chua co bang chung chac chan cho alias cua Minh.

Khong tu dong gan Tuan/Quang/XernesLucid cho Minh. Neu khong tim thay alias
cua Minh, ghi ro `ownership evidence unavailable` va giao cho Minh task doc,
QA, test harness hoac module co blast radius thap; khong dua ra khang dinh ve
ky nang cua ban ay.

## Pham vi gap phai tong hop

1. P0-00 den P0-03: implementation, runtime, regression test, evidence va
   phan acceptance chua dat.
2. Project, Task/Jira, Group, Message, Meeting, Action Item, Wiki,
   Notification, Audit va AI Insight.
3. Canonical ID, shareable deep link, back navigation, breadcrumb, preview co
   permission, contextual action va dieu huong hai chieu.
4. Meeting -> transcript -> AI job -> result -> TaskDraft -> human approval ->
   Task -> quay lai source Meeting.
5. AI worker lifecycle, provider/test provider, queue, retry, idempotency,
   cancellation, fallback, budget, audit va degraded state.
6. Privacy/retention/consent/DSAR/legal hold: UI, API, persistence, worker,
   enforcement va test.
7. Redis degraded mode, health/readiness, LiveKit local verification,
   configuration validation va runbook.
8. Error/loading/empty state, search, saved view, bulk action, undo,
   optimistic concurrency, versioning va rollback.
9. CI, security checks, migrations, test stability, generated frontend bundle,
   branch protection, CODEOWNERS, release va rollback.
10. Tat ca requirement P0/P1 trong Goal va Doc v4. P2 chi liet ke vao backlog,
    khong dua vao cam ket hai tuan tru khi la dependency bat buoc.
11. Cross-cutting business correctness: tenant isolation, RBAC, permission
    inheritance, validation, referential integrity, transaction boundary,
    idempotency, optimistic concurrency, duplicate submission, retry,
    cancellation, audit, notification va rollback.
12. Data edge cases: null/empty, duplicate, stale row version, missing relation,
    deleted/archived entity, large payload, pagination, filter/sort, timezone,
    Unicode, malformed input va unauthorized access.
13. Operational edge cases: process restart, worker restart, queue replay,
    provider timeout, Redis unavailable, partial failure, migration forward/
    backward compatibility, config missing va generated bundle stale.
14. Orphan detection: route khong reachable, endpoint khong co caller, UI action
    khong co handler, service khong duoc resolve, migration/entity khong duoc
    dung, test chi pass do mock bo qua implementation, doc item khong co code.

## Cach danh gia

Dung cac trang thai:

- Verified
- Implemented-Unverified
- Partial
- Spec-only
- Missing
- Environment-Blocked
- Out-of-scope
- Deprecated

Moi gap phai co:

- Gap ID on dinh.
- Module/luong nghiep vu.
- Hanh vi hien tai.
- Hanh vi mong muon.
- Loai gap.
- Severity va impact nguoi dung.
- Evidence file + line, test, endpoint, database hoac runtime.
- Requirement/acceptance/ADR Doc v4 lien quan.
- Dependency.
- Cach dong gap.
- Automated verification.
- Manual verification chi khi automation khong the thay the.
- Owner de xuat.
- Sprint de xuat.

Khong dung phan tram hoan thanh neu khong co tu so, mau so va danh sach item.
Khong danh dau Verified chi vi co entity, migration, endpoint stub, seed, UI
hoac test mock khong di qua implementation that.

## Definition of Done: chay dung va san sang nhan du lieu that

Moi chuc nang chi duoc gan `Verified` va moi task chi duoc dong khi dat tat ca
check ap dung duoi day:

1. Happy path chay end-to-end tu entry point nguoi dung/API den domain rule va
   persistence/output quan sat duoc.
2. Test dung synthetic/fixture data tren cung schema, validation, service,
   transaction va authorization path ma production se dung.
3. Khong hard-code ID seed, tenant, user, project, meeting, task hoac ket qua AI.
4. Thay fixture bang record moi hop le khong can sua code va khong phu thuoc
   thu tu seed.
5. Create -> read-back -> update/state transition -> reload/restart -> read-back
   giu ket qua dung; delete/disable/rollback duoc kiem tra neu nghiep vu co.
6. Validation, permission, duplicate, concurrency, retry va partial failure co
   ket qua dung nghiep vu, khong loi im lang va khong bao success gia.
7. Database constraint, foreign key, unique/index va transaction bao ve invariant
   quan trong; khong chi dua vao UI validation.
8. Tenant/RBAC test co ca allow va deny; khong ro ri du lieu qua ID truc tiep,
   search, preview, export, notification hoac AI context.
9. External dependency co contract test. Fake/sandbox adapter chi thay transport,
   khong bo qua domain flow; payload va error semantics phai tuong thich adapter
   that.
10. AI output duoc validate theo schema, trace ve source, co idempotency va human
    approval truoc moi mutation bi scope cam autonomous.
11. UI co loading/error/empty/success trung thuc, refresh khong mat state, deep
    link/back navigation va permission behavior dung acceptance.
12. Automated test that bai neu bo implementation hoac doi sai business rule;
    khong chap nhan test chi assert status 200/snapshot tinh.
13. Build, migration, unit, integration, contract va E2E lien quan pass trong
    clean environment, khong chi tren database may da co seed.
14. Co runtime evidence lap lai duoc va lenh test cu the trong context pack.
15. Doc v4/traceability/acceptance duoc cap nhat boi task duoc giao sau khi code
    merge, nhung khong duoc tu nhan production-ready neu external production
    integration chua duoc xac minh.

Voi du lieu that, ke hoach phai dam bao `data-agnostic readiness`: input that
chi can dap ung contract/schema da cong bo thi luong dung lai nguyen implementation
da test. Neu can special-case theo du lieu seed, task chua dat Definition of Done.

## Lap ke hoach hai dot trong hai tuan

Lap hai dot, moi dot 5 ngay lam viec:

- Dot 1 / Tuan 1: tai lap duoc, sua luong hong, ket noi P0 da co, bo sung test
  va moi truong local/CI co the chay lap lai.
- Dot 2 / Tuan 2: dong cac integration gap, QoL uu tien cao, evidence, hardening,
  document alignment va release verification.

Tong capacity danh nghia la 5 nguoi x 10 ngay = 50 person-days. Chi cam ket
toi da 40 person-days, giu 20% buffer cho review, conflict, bug va rework.
Neu Goal/Doc v4 vuot capacity, van phai inventory day du nhung chuyen phan vuot
capacity sang `Post-2-week remaining backlog`; khong ep tat ca thanh cam ket ao.

Moi task:

- Mot owner chinh, mot reviewer.
- Uoc luong 0.5 den 2 person-days; task lon hon phai tach.
- Mot ket qua kiem chung duoc va pham vi file ro.
- Uu tien doc lap de lam song song.
- Han che hai nguoi sua cung file trong cung dot.
- Co dependency va merge order.
- Du nho de chay bang Codex quota thuong hoac Gemini Pro; khong phu thuoc
  Codex Plus/Ultra hay mot context scan toan repo cho moi task.
- Automated test phai duoc agent chay truoc; chi giao nguoi dung cac buoc thu
  cong ma agent khong the thuc hien.

## Context pack bat buoc cho tung task

Moi task trong file Goal phai tu du context de mot agent moi co the bat dau ma
khong can hoi lai lich su thread:

1. Task ID va ten ngan.
2. Owner/reviewer va ly do phan cong dua tren Git evidence.
3. Gap ID, Goal item, requirement va acceptance Doc v4.
4. Hien trang da xac minh.
5. Ket qua can dat, viet bang ngon ngu nghiep vu de hieu.
6. File/module du kien duoc sua va file khong duoc cham vao.
7. Commit lien quan can doc truoc.
8. Cac buoc implementation co gioi han, khong micro-manage code.
9. Acceptance criteria theo Given/When/Then hoac checklist quan sat duoc.
10. Lenh build/test/lint/migration/E2E can chay.
11. Evidence phai nop trong PR.
12. Dependency, rui ro, rollback va merge order.
13. Out-of-scope de ngan scope creep.
14. Branch/PR title de xuat, theo prefix `codex/` neu agent Codex tao branch.

## Chien luoc chia viec va merge

- Uu tien ownership san co nhung khong de mot nguoi om toan bo AI/backend.
- Tach frontend, backend, integration test va docs neu giup giam conflict.
- Khong tach mot vertical flow thanh nhieu PR ma khong co interface contract.
- Chi ro task nao co the lam song song, task nao phai merge truoc.
- Lap ma tran file-overlap de canh bao conflict.
- Moi PR phai nho, co test va co cach rollback.
- Dinh nghia integration owner cho cuoi moi dot.
- Cuoi Dot 1 phai co checkpoint branch sync va runtime smoke.
- Cuoi Dot 2 phai co release candidate smoke, traceability update va danh sach
  remaining risks.

## Dau ra duy nhat can tao

Tao file moi, khong ghi de tai lieu v4 hien co:

`QALY_Docs_v4.0_Draft/16_QALY_GOAL_AND_2_WEEK_TEAM_EXECUTION_PLAN.md`

File nay phai chua theo thu tu:

1. Goal statement va Definition of Done.
2. Executive verdict ngan gon: he thong da lam duoc gi, gap lon nhat la gi.
3. Source/evidence baseline va commit dang audit.
4. Git ownership map cua 5 thanh vien.
5. Full Gap Register doi chieu Goal + Doc v4 + implementation/runtime.
6. Committed Scope trong 2 tuan va Post-2-week remaining backlog.
7. Dot 1/Tuan 1: task board theo ngay, owner, reviewer, effort, dependency.
8. Dot 2/Tuan 2: task board theo ngay, owner, reviewer, effort, dependency.
9. Context pack day du cho tung task.
10. File-overlap/conflict matrix va merge order.
11. Test/evidence matrix va cach kiem chung khong "ao".
12. Daily sync, PR/review policy, checkpoint va rollback plan.
13. Decision/questions chi danh cho Product Owner neu that su chan ke hoach.
14. Requirement Universe day du voi tong so va disposition tung REQ-ID.
15. Implementation Surface Universe voi tong so va disposition tung IMP-ID.
16. Bidirectional Traceability Matrix: REQ -> GAP/VERIFIED -> IMP -> TEST -> TASK.
17. Coverage Closure Report voi tu so, mau so, orphan count va ket qua PASS/FAIL.
18. Business Scenario Matrix bao gom happy path, deny path, boundary, failure,
    retry/concurrency va restart/read-back cho tung vertical workflow.
19. Real-data Readiness Checklist: schema/contract, data independence, tenant,
    permission, transaction, migration va adapter parity.

Dung tieng Viet de hieu, giai thich thuat ngu khi can. Bang co the dai nhung
khong duoc viet chung chung nhu "hoan thien AI", "toi uu backend" hoac
"kiem tra UI". Moi task phai gan voi gap, file, acceptance va test cu the.

## Trinh tu thuc hien

1. Doc Goal va Doc v4.
2. Audit Git ownership cua 5 nguoi.
3. Xac minh lai cac runtime finding quan trong, uu tien test/API tu dong.
4. Lap traceability va Full Gap Register.
5. Uoc luong capacity/dependency/conflict.
6. Chia task thanh hai dot va viet context pack.
7. Tao file Goal duy nhat neu tren.
8. Tu review file: khong gap nao bi bo quen, khong task nao qua lon, khong
   owner nao vuot capacity, khong hai task song song sua cung file ma khong co
   merge order.
9. Chay closure audit lan hai tu dau, khong tai su dung ket luan lan mot: doi
   chieu tong REQ-ID, IMP-ID, GAP-ID, TEST-ID va TASK-ID; liet ke orphan va sua
   file cho den khi tat ca closure gate bang 0.
10. Chay adversarial review: tim requirement an trong footnote/CSV/ADR, endpoint
    khong caller, UI action khong persistence, test mock bypass, config chi chay
    tren may cu va task co acceptance khong the chung minh.
11. Dung lai sau khi tao file va bao cao ngan gon. Khong implementation source.

## Dieu kien ket thuc lan chay

Chi ket thuc khi file Goal ton tai, co the giao viec truc tiep cho 5 nguoi,
moi task co du context de mot agent quota thuong xu ly doc lap va Coverage
Closure Report dat PASS voi tat ca orphan count bang 0. Coverage 100% o day la
100% inventory/disposition co mau so; khong duoc bien no thanh tuyen bo 100%
implementation.

Moi task implementation trong ke hoach phai dung Definition of Done `chay dung
va san sang nhan du lieu that` o tren. Khong danh dau Goal dai han Complete;
day chi la buoc lap execution plan hai tuan.
