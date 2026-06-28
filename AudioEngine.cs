using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace are_you_there
{
    class AudioEngine
    {
        // 1. 필요한 Windows API 및 상수 정의
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WM_QUIT = 0x0012;

        private delegate void WinEventDelegate(IntPtr hWinEvent, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEvent, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEvent);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint idThread, uint Msg, UIntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public System.Drawing.Point pt;
        }

        // 2. 필드 유지 (GC 방지)
        private static WinEventDelegate? _hookDelegate;
        private static IntPtr _hookHandle;
        private static uint _lastActivePID = 0;
        private static uint? _myPID = null;
        private static uint _messageLoopThreadId;
        private static bool _isRunning;

        // 볼륨 세팅값
        private static float focused = 1.0f;     // 100%
        private static float unfocused = 0.1f;   // 10%

        // 원래 볼륨 복구를 위한 딕셔너리
        private static readonly Dictionary<string, float> org_dict = new();
        private static readonly Dictionary<uint, float> org_process_dict = new();

        public static void SetVolume(float focused_input,  float unfocused_input)
        {
            focused = focused_input;
            unfocused = unfocused_input;
        }

        public static void StartEngine()
        {
            Console.WriteLine("ADHD 회의참여법 코어 엔진 시작...");

            // [추가] Ctrl+C 시그널 핸들러
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // OS의 즉시 강제종료를 일단 막음
                if (_hookHandle != IntPtr.Zero)
                {
                    UnhookWinEvent(_hookHandle);
                    RestoreOriginalVolumes();
                    Console.WriteLine("훅 해제 및 볼륨 복구 완료. 프로그램 종료.");
                }
                Environment.Exit(0); // 안전하게 정상 종료
            };

            if (_isRunning)
            {
                Console.WriteLine("이미 엔진이 실행 중입니다.");
                return;
            }

            _isRunning = true;
            _messageLoopThreadId = GetCurrentThreadId();
            if (!_myPID.HasValue)
            {
                _myPID = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            }

            // 기존 볼륨 세팅 저장
            SaveOriginalVolumes();

            // 훅 등록
            _hookDelegate = new WinEventDelegate(WinEventCallback);
            _hookHandle = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _hookDelegate, 0, 0, WINEVENT_OUTOFCONTEXT
            );

            if (_hookHandle == IntPtr.Zero)
            {
                Console.WriteLine("이벤트 훅 등록 실패!");
                return;
            }

            Console.WriteLine("훅 등록 완료. 창을 전환해보세요. (Ctrl+C를 누르면 종료됩니다)");

            // Console.ReadLine(); 대신 Windows 메시지 루프 실행
            // GetMessage는 큐에 메시지가 올 때까지 스레드를 대기 상태(Suspend)로 만들지만,
            // OS 이벤트(훅)가 오면 깨어나서 콜백을 실행하도록 연결해 줍니다.
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

        }

        public static void EndEngine()
        {
            if (!_isRunning)
            {
                Console.WriteLine("엔진이 실행 중이 아닙니다.");
                return;
            }

            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            RestoreOriginalVolumes();
            Console.WriteLine("훅 해제 및 볼륨 복구 완료. 프로그램 종료.");

            if (_messageLoopThreadId != 0)
            {
                PostThreadMessage(_messageLoopThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
                _messageLoopThreadId = 0;
            }

            _isRunning = false;
        }

        // 초기 볼륨 저장 함수
        private static void SaveOriginalVolumes()
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                string sessionId = session.GetSessionIdentifier;
                float currentVolume = session.SimpleAudioVolume.Volume;
                uint processId = session.GetProcessID;

                if (!org_dict.ContainsKey(sessionId))
                {
                    org_dict.Add(sessionId, currentVolume);
                }
                if (processId != 0 && !org_process_dict.ContainsKey(processId))
                {
                    org_process_dict.Add(processId, currentVolume);
                }
            }
        }

        private static void WinEventCallback(IntPtr hWinEvent, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            GetWindowThreadProcessId(hwnd, out uint currentPID);

            if (_myPID.HasValue && currentPID == _myPID.Value)
            {
                return;
            }

            if (currentPID == _lastActivePID) return;

            _lastActivePID = currentPID;

            AdjustVolume(currentPID);
        }

        // 메인 볼륨 조절 루틴
        private static void AdjustVolume(uint targetPID)
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;

            string targetProcessName = GetProcessNameSafe(targetPID) ?? string.Empty;

            for (int i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];

                uint processId = session.GetProcessID;
                if (processId == 0) continue;

                string sessionId = session.GetSessionIdentifier;
                float currentVolume = session.SimpleAudioVolume.Volume;
                string sessionProcessName = GetProcessNameSafe(processId) ?? string.Empty;
                bool isFocused = processId == targetPID || (!string.IsNullOrEmpty(targetProcessName) && targetProcessName.Equals(sessionProcessName, StringComparison.OrdinalIgnoreCase));
                float newVolume = isFocused ? focused : unfocused;

                if (!org_dict.ContainsKey(sessionId))
                {
                    org_dict.Add(sessionId, currentVolume);
                    if (processId != 0 && !org_process_dict.ContainsKey(processId))
                    {
                        org_process_dict.Add(processId, currentVolume);
                    }
                }

                session.SimpleAudioVolume.Volume = newVolume;
            }
        }

        // 프로그램 종료시 기존 볼륨으로 복구 루틴
        private static void RestoreOriginalVolumes()
        {
            Console.WriteLine("원래 볼륨으로 복구 중...");
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                string sessionId = session.GetSessionIdentifier;
                uint processId = session.GetProcessID;
                float currentVolume = session.SimpleAudioVolume.Volume;
                string displayName = session.DisplayName?.Trim();

                if (org_dict.ContainsKey(sessionId))
                {
                    float originalVolume = org_dict[sessionId];
                    session.SimpleAudioVolume.Volume = originalVolume;
                }
                else if (processId != 0 && org_process_dict.ContainsKey(processId))
                {
                    float originalVolume = org_process_dict[processId];
                    session.SimpleAudioVolume.Volume = originalVolume;
                }
            }
        }

        private static string? GetProcessNameSafe(uint pid)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch
            {
                return null;
            }
        }
    }
}
