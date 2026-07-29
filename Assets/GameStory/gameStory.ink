INCLUDE 2nd scene.ink
VAR scene = 1

=== PickUpthePhone ===
要是當初，我能更早發現就好了...

+{not test} [接聽] 
    # PhoneAction: PickUp
    # ChangePhoneImage: Incoming_Call
    -> Switch_scene2
    
+{test}[點擊測試物件後接聽] 
    # PhoneAction: PickUp
    # ChangePhoneImage: Incoming_Call
    -> Switch_scene2

+ [掛斷] 
    
    -> Hang_up_the_call



=== Answer_the_call ===
(文字)
來電


    -> Switch_scene2

=== Hang_up_the_call ===
掛斷
    -> Switch_scene2



=== Switch_scene2 ===
~ scene = scene + 1
//.# phone.prefab.smile
//切換至場景 {scene}
.#Skybox.Dawn
.#LightingData_Day
->Scene2_start


=== example ===
突然間，桌上的手機螢幕亮了起來。
# phone.prefab.incomingcall
# position(0.5, 1.2, -0.2)
# rotation(0, 180, 0)
# TargetKnot="Answer_the_call"
->END

=== test ===
測試
END
->DONE

=== intro_story ===
# DirectorObject.timeline.Timeline_IntroScene
這是一段開場對白，當進入這個節點時，會自動去 Resources 載入並播放特定的 Timeline。
NPC點點頭說道：「你終於來了。」
註：DirectorObject 是你場景上掛載 PlayableDirector 的物件名稱，Timeline_IntroScene 則是對應的 Timeline 資源檔名。
-> DONE