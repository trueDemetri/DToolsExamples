# DToolsExamples
Тут представлена пара инструментов из накопившейся коллекции своей разработки, которую я сейчас использую во всех своих проектах.
Некоторые из них, казалось бы, дублируют функционалы из фреймворков, которые я также применяю, но для отдельной
реализации были свои причины (чаще всего слишком большая “тяжесть” родных решений фреймворков и большое количество
бойлерплейта в них).
В описаниях ниже такие моменты будут отмечены. Поскольку для DI я использую Zenject, то в примерах понятия
***контекст*** и ***инсталлер*** обозначают соответствующие вещи из него.   

## Messenger
Это инструмент для отправки событий по принципу шины сигналов. В Zenject’е есть под такое дело SignalBus.
Но мне он не нравится из-за необходимости отдельно декларировать 
в инсталлерах каждый сигнал, который ты хочешь отправлять. Это бойлерплейт, который, тем не менее, не исключает непредвиденные ошибки 
на этапе выполнения при попытке где-то отправить незадекларированный сигнал.
Мне хотелось сделать что-то легковесное с возможностью проверки допустимости отправки конкретного события через
определенный мессенджер без декларирования сигналов, а сразу на этапе компиляции.

API получившейся тулзы представлен всего парой методов класса Messenger. Допустимость отправки определенных событий
через конкретный мессенджер определяется на этапе его объявления за счет базы события для него.
В примере ниже это показано подробнее.

Как я его обычно использую? Для каждого контекста, где нужны внутренние события, я объявляю свой мессенджер и
интерфейс события для него. Сам интерфейс пустой, нужен только как флаг, чтобы по ошибке не отправить
событие другого контекста.

Итак, в контексте проекта я создаю следующий мессенджер (он самого высокого уровня и доступен
на любой сцене в проекте, т.е. отправляет общеигровые события):
```c#
public interface IGameEvent
{}

public class GameMessenger : Messenger<IGameEvent>
{}
```
Делаем его доступным в контексте проекта:
```c#
public class ProjectInstaller : MonoInstaller
{
  public override void InstallBindings()
  {
    Container.Bind<GameMessenger>().AsSingle();
  }
}
```

И для примера объявим мессенджер для отправки чисто боевых событий (будут доступны
только на сцене боя)
```c#
public interface IBattleEvent
{}

public class BattleMessenger : Messenger<IBattleEvent>
{}
```
И, соответственно, в контексте сцены боя
```c#
public class BattleSceneInstaller : MonoInstaller
{
  public override void InstallBindings()
  {
    Container.Bind<BattleMessenger>().AsSingle();
  }
}
```
А теперь примеры использования этих мессенджеров для отправки событий.    
Допустим, у нас есть общеигровое событие загрузки какой-то сцены
```c#
public class SceneLoadedEvent : IGameEvent
{
  public readonly string SceneId;
  
  public SceneLoadedEvent(string sceneId)
  {
    SceneId = sceneId;
  }
}
```
А также чисто боевое событие убийства врага
```c#
public class EnemyKilledEvent : IBattleEvent
{
  public readonly IEnemy Enemy;
  
  public EnemyKilledEvent(IEnemy enemy)
  {
    Enemy = enemy;
  }
}
```
Через мессенджер GameMessenger мы можем отправить общеигровое событие SceneLoadedEvent
```c#
//...
gameMessenger.RaiseEvent(new SceneLoadedEvent("MetaGame"));
// Это всего лишь короткая запись для
//gameMessenger.RaiseEvent<SceneLoadedEvent>(new SceneLoadedEvent("MetaGame"));
```
Но вот отправить по ошибке чисто боевое событие через GameMessenger не получится (ограничения через констрейнт типа-параметра),
у них разные интерфейсы (ведь для боевых событий у нас есть BattleMessenger в контексте боя) Т.е.
```c#
gameMessenger.RaiseEvent(new EnemyKilledEvent("John Doe")) // ошибка !
```
И это будет видно сразу. Код даже не скомпилируется, а не вызовет исключение на этапе выполнения.

Подписка на событие производится следующим образом
```c#
gameMessenger.Subscribe<TEventType>(OnSceneLoaded, false);
```
Второй параметр можно опустить. Если его выставить в true, то подписчик получит событие раньше тех,
кто подписался без этого флага.   
Метод Subscribe возвращает IDisposable, через который потом можно отписаться от события.
```c#
public class Test : IDisposable
{
  private readonly IDisposable _subscription;
  
  public Test(GameMessenger gameMessenger)
  {
    _subscription = gameMessenger.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
  }
  
  public void Dispose()
  {
    _subscription.Dispose();
  }
  
  private static void OnSceneLoaded(SceneLoadedEvent evnt)
  {
    Debug.Log($"Scene {evnt.SceneId} loaded")
  }
}
```
Это особенно удобно, если использовать вещи наподобие расширений для IDisposable из UniRx. 
```c#
public class TestMono : MonoBehaviour
{
  public void Construct(GameMessenger gameMessenger)
  {
    gameMessenger.Subscribe<SceneLoadedEvent>(OnSceneLoaded)
      .AddTo(this);
  }
  
  private static void OnSceneLoaded(SceneLoadedEvent evnt)
  {
    Debug.Log($"Scene {evnt.SceneId} loaded")
  }
}
```
Для отписки в классе Messenger также есть метод Unsubscribe(), но он намного менее удобен.

### Рекомендации по использованию.
- Для исключения аллокаций памяти в частых событиях рекомендуется использовать структуры 
вместо классов для передачи информации.
    ```c#
  public struct WeaponShot : IPlayerEvent
    ```
  Боксинга при отправке таких событий (а также при подписке на них) не происходит из-за
простоты системы.
  

- Если событие не имеет параметров, то нет смысла создавать объект самого события через **new**, запрашивая выделение памяти.
  Достаточно использовать тип для идентификации события и передавать null (если параметр в RaiseEvent опустить, то так и произойдет).
```c#
battleMessenger.RaiseEvent<ParameterlessEvent>();
// Равносильно battleMessenger.RaiseEvent(new ParameterlessEvent()), но без лишней аллокации
```
## StateMachine
Это простая система для реализации конечных автоматов.
Представлена двумя классами.
### StateMachine<TStateBase>
Система для агрегации событий, обеспечения переходов между ними. В качестве типа-параметра задается база событий,
с которыми будет работать данный экземпляр. 

Интерфейс класса:
1. SetStates(TStateBase[] states)  
Тут задаются состояния, которые будут в данной стейт-машине. Объекты состояний, переданные сюда, потом
   переиспользуются внутри.
2. SetState<TStateBase>(object parameter = null, bool allowTransitionToSelf = false)  
Обеспечивает переход в указанное типом состояние, в которое при желании можно передавать какой-то параметр (чаще
   всего это не нужно; также важно помнить про боксинг при передаче типов-значений).  
   Второй необязательный параметр - это флаг разрешения перехода из состояния в себя же. По умолчанию выключен
   (показывает ошибку при попытке такого перехода и игнорирует её).
3. TStateBase CurrentState { get; } - возвращает текущее состояние.
4. Dispose() - производит очистку машины, выходящей из обращения. Очень удобно применять с Dispose()
   конкретных состояний. Машина вызывает их все. Например, это могут быть отписки от каких-то
   событий, которое использует состояние, освобождение внутренних ресурсов состояния и т.д.

### State\<TStateBase\>
Абстрактный класс, от которого наследуются все состояния. В наследниках его можно дополнять необходимым интерфейсом.
Интерфейс базового класса:
1. protected virtual OnEnter(TStateBase prevState, object arg)  
Переопределяем этот метод, если нужно указать поведение на вход в данное состояние. prevState содержит объект
   предшествующего состояния, из которого происходит переход. В arg содержится агрумент, переданный в SetState
   машины при смене события.
2. protected virtual OnExit(TStateBase nextState)  
   Переопределяем этот метод, для указания поведения на выход из данного состояния. nextState содержит объект
   следующего состояния, в которое происходит переход.
3. bool Active { get; }
   Показывает активно ли сейчас данное состояние (находится ли машина в нем). Применяется во внутренних
   подписках на события, которые могут сработать и при неактивном состоянии (это же можно сделать подписываясь
   на входе и отписываясь на выходе, только с известными небольшими накладными расходами, приносимыми этими действиями).
4. StateMachine<TEvent> StateMachine { get; }  
  Ссылка на машину, владеющую данным состоянием.
5. Dispose()  
   Указываем здесь, если нужно, логику очистки события, выходящего из обращения.


#### Пример использования состояний
Далее в качестве примера показана простая реализация логики работы оружия с кулдауном на перезарядку после
каждого выстрела.

Сначала указываем базу для состояний в стейт-машине. База обязательно должна быть унаследована от класса State.
```c#
public abstract class WeaponState : State<WeaponState>
{
  public virtual void Tick()
  {}
  
  public virtual void Shoot()
  {} 
}
```
Дальше создадим несколько состояний для реализации простейшей логики оружия
```c#
public class ReadyState : WeaponState
{
  public override void Shoot()
  {
    //... тут запускаем снаряд из оружия...
    // Затем
    StateMachine.SetState<ReloadingState>();
  }
}

public class ReloadingState : WeaponState
{
  private float _reloadingInterval;
  private float _exitTime;
  
  public ReloadingState(float reloadingInterval)
  {
    _reloadingInterval = reloadingInterval;
  }
  
  protected override void OnEnter(WeaponState prevState, object arg)
  {
    _exitTime = Time.time + _reloadingInterval;
  }
  
  public override Tick()
  {
    if (Time.time < _exitTime) return;
    
    StateMachine.SetState<ReadyState>();
  }
}
```
Теперь мы можем создать стейт машину под такие состояния и применять ее в классе оружия.
```c#
public interface IWeapon
{
  void Shoot();
}

public class Weapon : IWeapon, ITickable, IDisposable
{
  private readonly StateMachine<WeaponState> _stateMachine = new StateMachine<WeaponState>(); 
  
  public Weapon(float reloadingInterval)
  {
    _stateMachine.SetStates
    (
      new WeaponState[]
      {
        new ReadyState(),
        new ReloadingState(_reloadingInterval)      
      }    
    );
    
    _stateMachine.SetState<ReadyState>();
  }
    
  public void Shoot()
  {
    _stateMachine.CurrentState?.Shoot();
  }
  
  public void Tick()
  {
    _stateMachine.CurrentState?.Tick();
  }
  
  public void Dispose()
  {
    _stateMachine.Dispose();
  }
}
```
Наше оружие будет стрелять только в состоянии ReadyState, после чего переходить в кулдаун состоянием ReloadingState,
в котором стрелять нельзя.  
Машина состояний в таких случаях позволяет более элегантно выразить логику. Особенно это хорошо было бы заметно
в нашем примере, если бы в нем присутствовала стрельба очередями, отдельные кулдауны между очередями и обоймами.
В таком случае разделение поведения на отдельные состояния делает код понятным, в отличие от
заталкивания всей работы в один класс с необходимостью "жонглирования" кучей флагов для разных состояний.