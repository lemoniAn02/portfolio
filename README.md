# 양유정 포트폴리오

밤의 로우폴리 계곡을 따라 걸으며 소개 · 수상 · 기술 · 프로젝트(ROADY, 왕의 행차, 노디세이) · 연락처 전시를 만나는 탐험형 포트폴리오입니다.
three.js 한 파일로 돌아가고, 프로젝트 구간의 소품은 각 게임에서 실제로 쓴 에셋을 Unity에서 GLB로 내보낸 것입니다.

## 구조

| 경로 | 내용 |
| --- | --- |
| `index.html` | 페이지 전체. 마크업 · CSS · three.js 모듈 스크립트가 한 파일에 있습니다. |
| `img/` | 전시 갤러리 사진과 로고 (1280px JPEG / PNG) |
| `models/` | 프로젝트 에셋 GLB (`k-*` 왕의 행차, `n-*` 노디세이) |
| `tools/` | Unity 내보내기 스크립트, GLB 텍스처 축소, 아티팩트 빌드 |

외부 의존은 CDN의 three.js(0.160.0)와 Google Fonts뿐입니다.

## 실행

```powershell
python -m http.server 8765
```

브라우저에서 `http://localhost:8765` 을 엽니다. `file://` 로 열면 모듈 스크립트가 실행되지 않습니다.

## 내용 수정

- **정거장 추가**: `index.html` 의 `STATIONS` 배열에 `{ id, t }` 를 넣고, 같은 `id` 로 `.panel[data-station]`(전시 패널), 트레일 `<button data-go>`, 씬 안의 `hot(id, x, y, z)` 를 추가합니다.
- **채울 자리**: `<!-- TODO -->` 주석이 있는 곳 — 수상명, 이메일 · GitHub · 이력서, 프로젝트 링크.
- **사진**: `img/` 에 넣고 패널의 `.gallery img` 의 `src` 를 바꿉니다. 갤러리는 16:9 로 잘려 보입니다.
- **모델 교체**: `tools/README.md` 참고. 내보낸 GLB 는 `models/` 에 두고 `placeModel()` / `scatterModel()` 로 배치합니다.

## 작업 규칙

### 브랜치 (git-flow)

| 브랜치 | 역할 |
| --- | --- |
| `master` | 배포본. `develop` 에서만 머지하고 `vX.Y.Z` 태그를 답니다. (git-flow 원래 이름을 씁니다) |
| `develop` | 통합 브랜치. 모든 feature 브랜치의 MR 대상입니다. |
| `feature/<영역>-<주제>` | `develop` 에서 분기. 전시 하나 또는 주제 하나가 브랜치 하나입니다. |
| `fix/<주제>` | 버그 수정. `develop` 으로 MR. |
| `hotfix/<주제>` | 배포본 긴급 수정. `master` 에서 분기해 `master` 와 `develop` 양쪽에 머지. |

영역 이름: `intro` `awards` `skills` `roady` `kings` `noddsay` `home` `scene` `ui` `tools`

```text
feature/intro-about        자기소개 문안·사진
feature/awards             수상 내역
feature/project-roady      ROADY 전시
feature/project-kings      왕의 행차 전시
feature/project-noddsay    노디세이 전시
feature/scene-snow-light   설원 조명 조정 같은 씬 작업
```

```powershell
git switch develop
git switch -c feature/project-roady
# ... 작업, 커밋 ...
git push -u origin feature/project-roady
# MR: feature/project-roady → develop
```

### 커밋 메시지

```text
type(scope): 한글 요약        ← 50자 안팎, 끝에 마침표 없음

본문(선택): 무엇을 왜 바꿨는지. 이슈가 있으면 (#12)
```

- `type`: `feat` 새 기능 · `fix` 버그 · `content` 문안 · 사진 · 링크 · `style` 보이는 것만 · `refactor` 구조 · `chore` 도구 · 설정 · `docs` 문서
- `scope`: 위의 영역 이름
- 예: `content(awards): 자율 프로젝트 수상명 입력`, `feat(scene): 설원 눈 내림 추가`, `fix(ui): 모달 바깥 클릭으로 닫기`
- 작성자는 본인 git 계정 하나만 씁니다. `Co-Authored-By` 를 비롯해 도구 이름이 들어간 줄은 넣지 않습니다.

### MR / PR

- `develop ← feature/*` 를 `--no-ff` 로 머지해 브랜치 기록을 남기고, 머지 뒤 feature 브랜치는 지웁니다.
- 제목은 커밋 형식과 같게, 본문은 `.github/PULL_REQUEST_TEMPLATE.md` 를 따릅니다.
- 올리기 전 셀프 체크: 로컬 서버에서 데스크톱(1280) 과 폰(375) 폭 확인, 콘솔 에러 없음, 남은 TODO 표시.
- 릴리스: `develop → master` 머지 후 `git tag v0.2.0` 처럼 태그.
