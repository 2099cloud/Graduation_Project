// 定義全局變數，用來追蹤後面的結局走向
VAR scene = 0
桌上有一支手機
*[拿起手機]->PickUpthePhone

=== PickUpthePhone ===

要是當初，我能更早發現就好了...
    
* [接聽] 
    # PhoneAction: PickUp
    # ChangePhoneImage: Incoming_Call
    -> Answer_the_call

=== Answer_the_call ===
來電
    -> Switch_scene

=== Switch_scene ===
~ scene = scene + 1
切換至場景 {scene}
->END