using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = DebugM;

// 서비스 로케스터 구조
public static class ScriptM 
{

    #region Service Locator 함수들
    /// <summary>
    /// 싱글톤을 대신 하기 위해 만든 클래스
    /// </summary>
    private class ServiceEntry
    {
        public object Instance { get; set; }
        public UseSpace Lifetime { get; set; }
    }

    /// <summary>
    /// 서비스들이 들어가는 딕셔너리
    /// </summary>
    private static readonly Dictionary<Type, ServiceEntry> _services = new();


    /// <summary>
    /// 서비스 등록을 할때 Awake에서 지정해줘야 하는 것
    /// </summary>
    /// <param name="service"> 등록할 서비스</param>
    /// <param name="lifetime">멀티/전역 혹은 로컬</param>
    public static void Register<T>(T service, UseSpace lifetime = UseSpace.Global) where T : class
    {
        var type = typeof(T);
        if (_services.ContainsKey(type))
        {
            Debug.LogWarningWithTag("ServiceLocator", $"이미 등록된 서비스");
        }

        //전역 사용될 시 계속 들고 다니기
        if (lifetime == UseSpace.Global)
        {
            if (service is Component component)
            {
                // 최상위(Root) 오브젝트로 만든 뒤 DDOL 적용
                component.transform.SetParent(null);
                UnityEngine.Object.DontDestroyOnLoad(component.gameObject);
            }
            else if (service is GameObject go)
            {
                go.transform.SetParent(null);
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
        }
        _services[type] = new ServiceEntry()
        {
            Instance = service,
            Lifetime = lifetime
        };
    }

    /// <summary>
    /// 서비스 등록에서 수동적으로 제거 해야 할 필요가 있을 때
    /// </summary>
    public static void Unregister<T>() where T : class
    {
        _services.Remove(typeof(T));
    }

    /// <summary>
    /// 등록된 서비스 조회를 시도하여 성공 시 true와 인스턴스를 반환하고, 실패 시 에러 로그와 함께 false를 반환 if(!ScriptM.TryGet<T>(out T 변수명)) {retern;} 식으로 사용
    /// </summary>
    public static bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var entry))
        {
            service = entry.Instance as T;
            return true;
        }

        Debug.LogErrorWithTag("ServiceLocator", $"{typeof(T).Name}을 찾을 수 없음");
        service = null;
        return false;
    }

    /// <summary>
    /// 씬 이동시 로컬은 전부 해제하고 전역인 것만 등록한 상태로 들고 다닌다.
    /// </summary>
    public static void ClearSceneLocalServices()
    {
        List<Type> toRemove = new();
        foreach (var pair in _services)
        {
            if (pair.Value.Lifetime == UseSpace.Local|| pair.Value.Lifetime == UseSpace.Network)
            {
                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _services.Remove(toRemove[i]);
        }
    }

    /// <summary>
    /// 게임종료시 서비스 전부 해제 하기
    /// </summary>
    public static void Reset()
    {
        _services.Clear();
    }

    #endregion


    #region 중앙 메시지 매니저 함수들

    /// <summary>
    /// 메세지 집합소
    /// </summary>
    private static readonly Dictionary<Type, Action<IGameMessage>> _handlers = new();

    // 구독  
    public static void Subscribe<T>(Action<T> handler) where T : IGameMessage
    {
        Type type = typeof(T);
        if (!_handlers.ContainsKey(type))
        {
            _handlers[type] = null;
        }
        _handlers[type] += (msg) => handler((T)msg);
    }

    // 구독 해제
    public static void Unsubscribe<T>(Action<T> handler) where T : IGameMessage
    {
        Type type = typeof(T);
        if (_handlers.ContainsKey(type))
        {
            _handlers[type] -= (msg) => handler((T)msg);
        }
    }

    // 발행 (Send)
    public static void Send<T>(T message) where T : IGameMessage
    {
        Type type = typeof(T);
        if (_handlers.TryGetValue(type, out var handler))
        {
            handler?.Invoke(message);
        }
    }

    #endregion
}
