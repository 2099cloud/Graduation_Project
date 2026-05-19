=== Scene2_start ===
.# phone.prefab.smile
.#Area Light.prefab.Yellow
-> Task_Assignment

=== Play_Animation ===
 // 注意這裡前面有一個空格，用來帶出標籤
 # phone.timeline.incoming_ring
 
 // 播完動畫後，鎖定全場景點擊，逼玩家專心看演出
 # InteractableObject.non-interactable
-> DONE

=== Task_Assignment ===
 // 派發任務：讀取應門項目
.#Task.text.應門
-> DONE

=== Delivery_man ===
 外送員站在門口，看起來有些焦慮。
-> DONE