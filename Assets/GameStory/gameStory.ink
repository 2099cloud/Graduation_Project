INCLUDE 1st scene.ink
INCLUDE 2nd scene.ink
INCLUDE 9th scene.ink

VAR scene = 1

//-----Plot-----
=== prologue ===
.#Main Character.SetActive.false
.#Old_Brother.SetActive.false
.#LightingData_Night
.#FreeLook Camera(3P).SetActive.false
.#Animation.timeline.0
.#phone.mode.Interactor

->DONE

===Title===
.#Animation.timeline.Title
-> Story1_1




//-----function-----

=== PickUpthePhone ===
要是當初，我能更早發現就好了...
+[接聽] 
    //# PhoneAction: PickUp
    //# ChangePhoneImage: Incoming_Call
    .#fade.Out
    -> Title

+[掛斷]
    -> DONE

===test===
.#Animation.timeline.5
-> DONE



//--------------------------------------------------
##Syntax##

#phone.prefab.smile
#phone.prefab.incomingcall
#phone.TargetKnot="Test"
#Front door.TargetKnot="Answer_the_door"
# DeskBox.mode.Inspector
#fade
# Main Character.position(-7.83, 0.345, 5.95)
# Main Character.rotation(0, 0, 0)

# DirectorObject.timeline.Timeline_IntroScene
這是一段開場對白，當進入這個節點時，會自動去 Resources 載入並播放特定的 Timeline。
NPC點點頭說道：「你終於來了。」
註：DirectorObject 是你場景上掛載 PlayableDirector 的物件名稱，Timeline_IntroScene 則是對應的 Timeline 資源檔名。