# tools

## Unity 에셋 → GLB (`unity/PortfolioGlbExport.cs`)

Unity 프로젝트의 프리팹 · 모델을 GLB 로 내보내는 임시 에디터 스크립트입니다. 메시를 루트 공간에 구워 넣고, Unity 왼손 좌표를 glTF 오른손 좌표로 바꾸고, 머티리얼 색과 메인 텍스처(PNG), 정점 색을 담습니다.

1. 대상 프로젝트에 `Assets/PortfolioGlbExportTmp/Editor/PortfolioGlbExport.cs` 로 복사합니다. (끝나면 폴더와 `.meta` 를 지우고, Unity 가 바꾼 `ProjectSettings/EditorBuildSettings.asset` 은 `git checkout` 으로 되돌립니다.)
2. 작업 목록 파일을 만듭니다. 한 줄에 하나:

   ```text
   에셋경로|출력이름
   에셋경로|출력이름|mat=*:머티리얼경로            # 모든 렌더러에 그 .mat 적용 (노드이름정규식:경로 도 가능, ; 로 여러 개)
   에셋경로|출력이름|클립경로|초                    # 스킨 메시를 그 애니메이션 시점 포즈로 구워 내보내기
   ```

   예시는 `unity/kings-jobs*.txt`, `unity/noddsay-jobs*.txt`.
3. 에디터를 닫은 상태에서 배치 모드로 실행합니다.

   ```powershell
   $env:PORTFOLIO_GLB_JOBS = "C:\path\jobs.txt"
   $env:PORTFOLIO_GLB_OUT  = "C:\DEV\portfolio\models"
   $env:PORTFOLIO_GLB_SKIP = "Char0"          # 메시 에셋 경로에 이 글자가 들어간 렌더러는 건너뜀 (쉼표로 여러 개)
   & "C:\Program Files\Unity\Hub\Editor\<버전>\Editor\Unity.exe" -batchmode -projectPath <프로젝트> -executeMethod PortfolioGlbExport.Run -logFile export.log -quit
   ```

   결과와 삼각형 수는 출력 폴더의 `_report.txt` 에 적힙니다.

주의: FBX 임포트 배율이 메시에 들어 있지 않은 경우가 많아 크기가 제각각입니다(나무 0.04, 캐릭터 100). `placeModel()` 의 `scale` 로 맞춥니다.

## `glbshrink.py`

```powershell
python tools/glbshrink.py models 512
```

GLB 안의 PNG 텍스처를 최대 512px JPEG(알파가 있으면 PNG) 로 바꿔 파일을 줄입니다.

## `glbinfo.py`

```powershell
python tools/glbinfo.py models
```

각 GLB 의 크기, 삼각형 수, 노드 이름, 경계 상자를 출력합니다. 배치할 때 `scale` 을 정하는 데 씁니다.

## `build_artifact.py`

```powershell
python tools/build_artifact.py
```

`index.html` 을 claude.ai 아티팩트용 복사본 `dist/artifact.html` 로 만듭니다(문서 뼈대와 import map 제거, CDN `+esm` 경로, 모델 참조를 `.wasm` 이름으로). 아티팩트 호스트가 `.glb` 를 받지 않아 같은 바이트를 `.wasm` 이름으로 올립니다.
