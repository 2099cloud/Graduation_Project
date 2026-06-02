=== Scene2_start ===
.# phone.prefab.smile
.#phone.TargetKnot="Test"
//.#Area Light.prefab.Yellow
.#Front door.TargetKnot="Answer_the_door"
.#Task.text.應門

-> Task_Assignment


=== Play_Animation ===
 // 注意這裡前面有一個空格，用來帶出標籤
 # phone.timeline.incoming_ring
 
 // 播完動畫後，鎖定全場景點擊，逼玩家專心看演出
 # InteractableObject.non-interactable
-> DONE

=== Task_Assignment ===
 // 派發任務：讀取應門項目
//.#Task.text.應門
-> DONE

=== Answer_the_door===
+[應門?]
.#fade
->Receive_package
+[不理會]
->DONE

=== Receive_package ===
.# Main Character.position(-7.83, 0.345, 5.95)
.# Main Character.rotation(0, 0, 0)
 外送員站在門口。
 END
-> DONE

=== Test ===
這是您的包裹，請您簽收
+[接收包裹]
你收到了一個包裹，上面印有...
.#Courier.SetActive.false
-> DONE

=== fade ===
.#fade
->DONE
