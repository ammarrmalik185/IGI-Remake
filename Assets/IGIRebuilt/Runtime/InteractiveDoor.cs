using UnityEngine;
namespace IGIRebuilt {
 public sealed class InteractiveDoor : MonoBehaviour {
  public Vector3 slide; public float angle,seconds=1; public bool isGate; public InteractiveDoor partner;
  public bool IsOpen {get;private set;} public float Fraction {get;private set;}
  Vector3 closedPosition; Quaternion closedRotation; Collider[] panels; bool initialized;
  void Awake(){Initialize();}
  public void Initialize(){if(initialized)return;closedPosition=transform.localPosition;closedRotation=transform.localRotation;panels=GetComponentsInChildren<Collider>();initialized=true;}
  public void Toggle(){Initialize();SetOpen(!IsOpen);if(partner!=null)partner.SetOpen(IsOpen);}
  public void SetOpen(bool open){Initialize();IsOpen=open;}
  void Update(){Advance(Time.deltaTime);}
  public void Advance(float dt){
   Initialize();float next=Mathf.MoveTowards(Fraction,IsOpen?1:0,dt/Mathf.Max(.4f,seconds));if(Mathf.Approximately(next,Fraction))return;
   Vector3 delta=transform.TransformDirection(slide)*(next-Fraction);
   // Stop a moving panel before its swept volume reaches a player or guard.
   foreach(var panel in panels){var b=panel.bounds;var sweep=b;sweep.Encapsulate(new Bounds(b.center+delta,b.size));
    foreach(var hit in Physics.OverlapBox(sweep.center,sweep.extents+Vector3.one*.08f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
     if(hit.GetComponentInParent<Health>()!=null)return;
   }
   Fraction=next;transform.localPosition=closedPosition+closedRotation*slide*Fraction;transform.localRotation=closedRotation*Quaternion.Euler(0,angle*Fraction,0);
  }
  public static InteractiveDoor Find(FirstPersonMotor player){
   if(player==null||player.eye==null||player.IsClimbing)return null;
   // Only the first solid surface under the crosshair is usable: walls cannot activate a door behind them.
   if(Physics.Raycast(player.eye.transform.position,player.eye.transform.forward,out var hit,3,1<<DemoWorld.WorldLayer,QueryTriggerInteraction.Ignore))return hit.collider.GetComponentInParent<InteractiveDoor>();
   return null;
  }
 }
}
