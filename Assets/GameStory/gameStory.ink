INCLUDE 2nd scene.ink
VAR scene = 0


=== PickUpthePhone ===
要是當初，我能更早發現就好了...

+ [接聽] 
    # PhoneAction: PickUp
    # ChangePhoneImage: Incoming_Call
    -> Answer_the_call

+ [掛斷] 
    
    -> Hang_up_the_call



=== Answer_the_call ===
(文字)
來電


    -> Switch_scene

=== Hang_up_the_call ===
掛斷
    -> Switch_scene

=== test ===
人家是個他喵的測試用節點
//->DONE
->END

=== Switch_scene ===
~ scene = scene + 1
切換至場景 {scene}
->END


