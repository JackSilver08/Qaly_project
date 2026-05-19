# README - Cách render sơ đồ QALY v2.3

## 1. Mermaid

Các file Mermaid nằm ở:

```text
docs/diagrams/mermaid/*.mmd
```

Cách dùng nhanh:

1. Mở Mermaid Live Editor hoặc extension Mermaid trong VS Code.
2. Copy nội dung file `.mmd`.
3. Export PNG/SVG rồi chèn vào báo cáo Word/PDF.

Khi viết báo cáo Markdown, có thể copy block trong `QALY_UML_Diagram_Specification_v2.3.md` vì file này đã chứa code fence dạng `mermaid`.

## 2. PlantUML

Các file PlantUML nằm ở:

```text
docs/diagrams/plantuml/*.puml
```

Dùng PlantUML khi cần đúng notation UML cho use case/component/deployment. Có thể render bằng VS Code PlantUML extension, IntelliJ PlantUML plugin, hoặc command PlantUML nếu máy đã cài.

## 3. Khuyến nghị chèn vào báo cáo tốt nghiệp

Thứ tự chèn nên dùng:

1. System Context
2. Overall Use Case
3. Module Use Case theo phần chức năng
4. Class Diagram
5. Core ERD SQL Server
6. Task State Machine
7. Activity Task Lifecycle
8. Sequence Create Task
9. Sequence Task Status Approval
10. Component Diagram
11. Deployment Diagram
12. AI/RAG và Import diagrams nếu báo cáo có module AI

## 4. Checklist nghiệm thu sơ đồ

- Có caption dưới mỗi sơ đồ.
- Có giải thích actor, boundary, service, database.
- Có mapping sang use case/API/table/test case trong `QALY_Diagram_Traceability_v2.3.csv`.
- Không chèn diagram quá nhỏ; nếu rộng, xuất SVG hoặc để landscape page.
