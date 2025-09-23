### GradientTextureGenerator
유니티 에디터에서 사용할 수 있는 그라디언트 텍스처 생성기

<img width="527" height="738" alt="image" src="https://github.com/user-attachments/assets/0aaa4c69-ec0e-44af-829a-21cefd8b128e" />


### 주요기능
1. 그라디언트 방향 제어
- 선형: 0-360도 자유로운 방향 설정
- 원형: 중심에서 바깥으로 퍼지는 그라디언트
- 대각선: 대각선 방향 그라디언트
- 다이아몬드: 다이아몬드 형태 그라디언트

2. 그라디언트 시작 지점 조절
- 시작 지점과 끝 지점을 0-1 범위에서 정밀 조절
- 그라디언트 커브를 사용한 비선형 전환 효과

3. PNG 내보내기
- Assets/Textures 폴더에 자동 저장
- 텍스처 임포트 설정 자동 적용
- 알파 채널 지원

추가 기능:
- 실시간 미리보기: 설정 변경 시 즉시 확인
- 다양한 해상도: 64x64부터 2048x2048까지 지원
- 알파 그라디언트: 투명도 그라디언트 별도 제어
- 커브 에디터: 비선형 그라디언트 전환

사용 방법:
- Assets/Editor 폴더에 스크립트 저장
- 유니티 메뉴에서 Tools > 그라디언트 텍스처 생성기 선택
- 원하는 설정으로 조절
- "PNG로 내보내기" 버튼 클릭

------

### AutoKeystoreSHAExtractor
Unity 프로젝트용 Android 키스토어 SHA 핑거프린트 자동 추출 도구

<img width="387" height="576" alt="image" src="https://github.com/user-attachments/assets/f4e1529a-5767-4c8e-be26-57ef94cfb6fa" />

Firebase Authentication 설정 시 필요한 SHA1/SHA256 해시값을 쉽고 빠르게 추출할 수 있는 Unity Editor 확장 도구입니다.
✨ 주요 기능

🔍 Keytool 자동 탐색: Unity 설치 경로에서 keytool.exe를 자동으로 찾습니다
🏗️ 다중 환경 지원: Development, Test, Production 환경을 동시에 처리
🚀 일괄 처리: 모든 키스토어를 한 번에 처리하거나 개별 처리 가능
📋 원클릭 복사: SHA 값을 클립보드에 바로 복사
🌐 Firebase 연동: Firebase Console로 바로 이동
📁 파일 관리: 키스토어 폴더 빠른 접근

🛠️ 요구사항

Unity 2019.4 이상
Android Build Support 모듈 설치
Java JDK (Unity와 함께 설치됨)

📦 설치 방법

AutoKeystoreSHAExtractor.cs 파일을 프로젝트의 Editor 폴더에 복사

   Assets/
   └── Editor/
       └── AutoKeystoreSHAExtractor.cs

Unity 에디터에서 Tools > Auto Keystore SHA Extractor 메뉴로 실행

🚀 사용법
1. 도구 실행
Unity 메뉴바에서 Tools > Auto Keystore SHA Extractor 선택
2. 프로젝트 경로 설정
기본 경로: C:\UnityContents\mascoz-mobile-unity
3. 키스토어 파일 준비
다음 경로에 키스토어 파일들을 배치:
Assets/Keystores/
├── Dev.keystore      # 개발용
├── Test.keystore     # 테스트용
└── Prod.keystore     # 배포용
4. SHA 추출 실행

🚀 Extract All SHA Keys: 모든 환경 동시 처리
Dev Only / Test Only / Prod Only: 개별 환경 처리

5. 결과 복사 및 사용

각 환경별 SHA1/SHA256 값이 표시됩니다
📋 Copy 버튼으로 클립보드에 복사
🌐 Firebase Console 버튼으로 Firebase로 이동

🔧 고급 설정
커스텀 경로 설정
csharp// InitializeKeystoreConfigs() 메서드에서 경로 수정 가능
new KeystoreConfig("Custom",
    "C:\\Custom\\Path\\MyKeystore.keystore",
    "myalias", "mypassword", "mypassword")
Keytool 수동 경로 지정
Keytool 자동 탐색이 실패할 경우, 다음 경로들을 확인하세요:

Unity Hub: C:\Program Files\Unity\Hub\Editor\[버전]\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe
Unity 직접 설치: C:\Program Files\Unity\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe

🐛 문제 해결
Keytool을 찾을 수 없는 경우

Android Build Support 모듈이 설치되어 있는지 확인
Unity Hub에서 Android Build Support 재설치
Java JDK 수동 설치 후 Unity Preferences에서 JDK 경로 설정

키스토어 파일을 찾을 수 없는 경우

파일 경로가 올바른지 확인
파일 확장자가 .keystore인지 확인
파일 권한 확인

SHA 추출 실패

키스토어 비밀번호가 올바른지 확인
Alias 이름이 정확한지 확인
키스토어 파일이 손상되지 않았는지 확인

### EnvBuildManager
### EnvSwitcher
