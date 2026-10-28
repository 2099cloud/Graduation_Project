===Scene10_Settings===
#Old_Brother.TargetKnot="check_ending1"
.#Food.SetActive.true
->DONE


=== Story10_1 ===
.#Animation.timeline.10-1
哥哥：早飯趁熱吃吧。
哥哥：對了，最近有個不錯的投資機會，你手邊有幾萬塊能先借我嗎？
->Story10_1_Choice1

=== Story10_1_Choice1 ===
+弟弟：我很挺你想多賺錢的想法。但能先跟我聊聊這個投資的內容嗎？
->Story10_1_Answer.Answer1

+弟弟：你怎麼突然要借這麼多錢？這聽起來有點太急了。
->Story10_1_Answer.Answer1

+弟弟：你居然想找我借錢去投資？這絕對是詐騙你別想了。
->Story10_1_Answer.Answer1

===Story10_1_Dialog2 ===
哥哥：我跟導師確認過了。這次只要再加碼就能拿到高回報。
->Story10_1_Choice2

=== Story10_1_Choice2 ===
+弟弟：我知道你想補回先前的虧損，但我還是很擔心你的資金安全。
->Story10_1_Answer.Answer2

+弟弟：幾萬塊不是個小數目。你不能單靠導師一句話就借錢。
->Story10_1_Answer.Answer2

+弟弟：你已經執迷不悟到要借錢了嗎？我絕對不可能拿錢給你砸。
->Story10_1_Answer.Answer2

===Story10_1_Dialog3 ===
哥哥：我們可是親兄弟。你連這點信任都不願意給我嗎？
->Story10_1_Choice3

=== Story10_1_Choice3 ===
+弟弟：我完全相信你這個人。但我不信任那些想利用你的陌生人。
->Story10_1_Answer.Answer3
+弟弟：我不是不信任你。我只是覺得利息高得不尋常。
->Story10_1_Answer.Answer3
+弟弟：這跟信任沒有關係。事實就是你現在正被詐騙集團操控。
->Story10_1_Answer.Answer3

===Story10_1_Dialog4 ===
哥哥：社群裡的群友大家都在獲利。這不可能會有假的。
->Story10_1_Choice4



=== Story10_1_Choice4 ===
+[我知道這聽起來很真實。但你願不願意先暫停轉帳？我們一起再查證一次。]弟弟：我知道這聽起來很真實。但你願不願意先暫停轉帳？我們一起再查證一次。
->Story10_1_Answer.Answer4
+[既然你這麼有把握。不如你先試著把獲利全額提現看看。]弟弟：既然你這麼有把握。不如你先試著把獲利全額提現看看。
->Story10_1_Answer.Answer4
+[你就是被親切的手法給蒙蔽了。等錢轉過去你就準備抱頭痛哭。]弟弟：你就是被親切的手法給蒙蔽了。等錢轉過去你就準備抱頭痛哭。
->Story10_1_Answer.Answer4

===Story10_1_Dialog5 ===
哥哥：你是不是覺得我很笨？連基本的是非對錯都分不出來？
->Story10_1_Choice5

=== Story10_1_Choice5 ===
+[你很聰明也很善良。這正是詐騙集團最常利用的特質。這跟智商完全無關。]弟弟：你很聰明也很善良。這正是詐騙集團最常利用的特質。這跟智商完全無關。
->Story10_1_Answer.Answer5
+[我只是怕你被話術騙了。現在詐騙手法真的很多。]弟弟：我只是怕你被話術騙了。現在詐騙手法真的很多。
->Story10_1_Answer.Answer5
+[你如果真的聰明，就不會連這麼明顯的套路都看不出。]弟弟：你如果真的聰明，就不會連這麼明顯的套路都看不出。
->Story10_1_Answer.Answer5

===Story10_1_Dialog6 ===
哥哥：好不容易看到一個希望，你非要現在潑我冷水嗎？
->Story10_1_Choice6

=== Story10_1_Choice6 ===
+[我很感謝你為家裡的付出。但因為關心你，我真的不想你冒險。]弟弟：我很感謝你為家裡的付出。但因為關心你，我真的不想你冒險。
->Story10_1_Answer.Answer6
+[你的心意大家都知道。但盲目投入只會造成更大的負擔。]弟弟：你的心意大家都知道。但盲目投入只會造成更大的負擔。
->Story10_1_Answer.Answer6
+[隨便你怎麼想。反正你要借錢去送死，我絕不幫忙。]弟弟：隨便你怎麼想。反正你要借錢去送死，我絕不幫忙。
->Story10_1_Answer.Answer6



===Story10_1_Answer===

=Answer1
哥哥：這個機會真的很難得。下週就能連本帶利還給你。
->Story10_1_Dialog2

=Answer2
哥哥：你這樣講是不相信我的判斷嗎？我難道會害自己嗎？
->Story10_1_Dialog3

=Answer3
哥哥:對方人很好，每天都關心我們，也會常常給我們投資指導，怎麼可能會是騙子？
->Story10_1_Dialog4

=Answer4
哥哥:你太疑神疑鬼了。這樣只會讓我錯過最佳入場時機。
->Story10_1_Dialog5

=Answer5
哥哥:我也只是想讓家裡生活好過一點。難道這樣做也有錯嗎？
->Story10_1_Dialog6

=Answer6
哥哥:算了，我知道你是為我好。這筆錢我先不加碼了。
->Decide_to_Verify2


=== Decide_to_Verify2 ===
.#fade.In
或許我應該再找點資料給他看，這樣或許對他而言比較有說服力。
也查查看這種狀況下該怎麼溝通比較有效好了...唉...
.#Task.text.查資料
->DONE
