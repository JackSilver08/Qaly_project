# Task tach rieng: No ky thuat ve phan quyen project va bao mat nghiep vu

> File nay tach cac bug/debt doc lap ra khoi plan timeline task. Muc tieu la tranh viec feature timeline lam nua voi roi bo sot cac loi quyen truy cap co san.

## 1. Ly do tach rieng

Quan ly timeline task la feature moi. Nhung trong luc phan tich co mot so loi/rui ro doc lap:

- API doc du lieu project chua check project membership day du.
- Webhook la be mat day du lieu ra ngoai, khong nen de member thuong quan ly.
- Role he thong dang bi dung lan voi role trong project.
- Logic phan quyen dang rai trong controller/service, kho test va de sot.

Nhung viec nay nen la ticket rieng, co acceptance criteria rieng, khong gop vao timeline feature.

## 2. Ticket 1 - Chan leak Wiki theo project

Van de:

- `WikiController.GetPages` chi yeu cau user dang nhap.
- Neu user biet `projectId`, co nguy co doc wiki project ma user khong thuoc ve.

Huong sua:

- Tao policy/service kiem tra user co quyen doc project.
- `GET /api/projects/{projectId}/wiki` phai goi policy truoc khi tra data.
- Private/internal wiki sau nay phai check visibility rieng.

Tieu chi nghiem thu:

- User khong thuoc project goi API wiki bi 403 hoac 404.
- User thuoc project xem duoc wiki hop le.
- Admin xem duoc theo quyen he thong.

## 3. Ticket 2 - Gioi han quyen quan ly Webhook

Van de:

- Webhook co the gui du lieu noi bo ra URL ben ngoai.
- Member thuong khong nen mac dinh tao/sua/xoa/test webhook.

Huong sua:

- Chi PM/ProjectOwner/System Admin duoc quan ly webhook.
- Neu can linh hoat, them permission `CanManageWebhooks`.
- Tat ca create/update/delete/test webhook phai ghi audit log.

Tieu chi nghiem thu:

- Member thuong khong co quyen tao webhook.
- PM/ProjectOwner tao webhook duoc.
- User duoc cap `CanManageWebhooks` tao webhook duoc neu sau nay co permission nay.
- Audit log ghi duoc URL, event, user thao tac.

## 4. Ticket 3 - Tach role he thong va role project

Van de:

- Neu dung role he thong de dien ta Dev/Tester/PM thi sai nghiep vu.
- Mot user co the giu role khac nhau theo tung project.

Huong sua:

- Global user role chi la `Admin/User`.
- Project role nam trong `ProjectMember.Role`.
- Tat ca check PM/Developer/Tester phai di qua project membership, khong dung `User.Role`.

Tieu chi nghiem thu:

- User A co the la Developer o project 1 va Tester o project 2.
- Quyen trong project 1 khong anh huong project 2.
- Admin he thong van co quyen quan tri rieng.

## 5. Ticket 4 - Gom project permission vao policy service

Van de:

- Logic quyen dang co nguy co rai trong controller/service.
- De tao bug khi them feature moi nhu timeline, wiki, webhook, customer portal.

Huong sua:

- Tao `IProjectAccessPolicy` hoac mo rong policy hien co.
- Cac ham can co:
  - `CanAccessProjectAsync`
  - `CanManageProjectAsync`
  - `CanViewProjectTimelineAsync`
  - `CanViewTaskRiskAsync`
  - `CanManageWebhooksAsync`
  - `CanReadWikiAsync`

Tieu chi nghiem thu:

- Controller khong tu viet dieu kien role phuc tap.
- Co unit test cho tung rule quyen.
- Cross-project access bi chan dong nhat.

## 6. Yeu cau ngon ngu khi code

Khi trien khai cac ticket nay, tat ca loi tra ve cho UI, toast, label, notification phai la tieng Viet ro rang.

Vi du:

- `Ban khong co quyen xem wiki cua du an nay.`
- `Chi PM hoac nguoi duoc cap quyen moi co the quan ly webhook.`
- `Thanh vien nay chua duoc cap quyen xem timeline du an.`

