# PROJECT DIAGRAMS

## 1. Project Namespace & Module Dependency
*(Vì đây là kiến trúc Monolith Single-Project, sơ đồ thể hiện luồng phụ thuộc giữa các thành phần không gian tên (Namespaces) bên trong dự án).*

```mermaid
graph TD
    UI[Controllers & Views] --> S[Services]
    UI --> EF[Data / ParishDbContext]
    S --> EF
    S --> DTO[Models.DTOs]
    S --> ENT[Models.Entities]
    UI --> DTO
    UI --> ENT
    EF --> ENT
    BG[BackgroundJobs / Hangfire] --> S
    BG --> EF