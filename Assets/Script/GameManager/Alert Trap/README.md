# Trap Countdown Alert

ลาก `TrapCountdownAlert.prefab` ไปเป็นลูกของ GameObject ที่ติด `TurnArrowTrap`
หรือ `TurnSpikeTrap` แล้วกด Play ป้ายจะอยู่เหนือกึ่งกลางโมเดลและหันเข้าหากล้อง
เลือกกับดักได้ที่ Target Object (ลาก Prefab instance จาก Hierarchy รองรับสคริปต์อยู่ในลูก)
หรือ Target Trap (ลาก component โดยตรง) ไม่ต้องให้ป้ายเป็นลูกกับดักเสมอไป
ต้องเลือกตัวที่วางอยู่ใน Scene เพราะ Prefab asset ใน Project ไม่มีสถานะเทิร์นขณะเล่น

- ใช้ TurnGameManager และ Interval Turns ของกับดักจริง ไม่ต้องตั้งเวลาซ้ำ
- เลข 1 หมายถึงกับดักจะทำงานใน Trap Phase ตอนจบรอบปัจจุบัน
- ถ้าเว้นช่องรูปไว้ จะใช้ตัวเลข สีขาวเมื่อเหลือมากกว่า 2 รอบ สีส้มเมื่อเหลือ 2 รอบ และสีแดงเมื่อเหลือ 1 รอบ
- เมื่อกับดักเริ่มทำงาน ป้ายจะขยายและจางจนหาย แล้วกลับมานับรอบใหม่เมื่อเอฟเฟกต์และการทำงานของกับดักจบแล้ว
- ถ้าหนามยังยกค้าง ป้ายจะกลับมาเมื่อหนามลดลง
- เมื่อกับดักปิดใช้งานหรือเกมจบ ป้ายจะซ่อนเอง
- ป้ายไม่รับ Raycast และไม่มี GraphicRaycaster จึงไม่ขวางการคลิกช่องเดิน

ตั้งค่าใน Inspector ของ `TrapCountdownAlert` ที่ prefab หรือ instance ใน Hierarchy

- **Placement** — เว้น **Position Anchor** ไว้เพื่อยึดตามโมเดล หรือเลือก Transform เช่น `Fire Point` เพื่อระบุจุดยึดเอง ใช้ **Screen Gap** กำหนดช่องว่างแนวตั้งเป็นพิกเซล และใช้ **Pixel Scale** ปรับขนาดป้ายบนจอโดยรวม
- **Follow Target** — ปิดเพื่อขยับ `Countdown Canvas` เองใน Hierarchy ป้ายยังชดเชยสเกลตามกล้อง
- **Appearance** — ใส่ **TMP Font Asset** ใน **Font Override** หรือเว้นไว้เพื่อใช้ฟอนต์ของ `Countdown` ซึ่งตั้งไว้เป็น `LiberationSans SDF` ใส่ Sprite ใน **State Sprites** แยกเป็น **Normal** เมื่อเหลือมากกว่า 1 รอบ, **Warning** เมื่อเหลือ 1 รอบ และ **Active** ตอนเริ่มทำงาน ช่องรูปที่ว่างจะแสดงตัวเลข โดย **Active** ใช้เลข 1
- **Activation Animation** — ปรับ **Activation Duration** เป็นวินาทีและ **Activation Scale** เป็นตัวคูณขนาด

ใน Assets/Scenes/Mon/ModelTest.unity วางป้ายไว้กับกับดักธนูฝั่งปลายทางแล้ว

ระบบนี้รองรับกับดักแบบเทิร์นที่สืบทอด TurnTrap เท่านั้น ไม่ใช่ ArrowTrap รุ่นเก่าที่นับวินาที
สคริปต์อ่านสถานะเพื่อแสดงผล ไม่ได้สร้าง UI ใหม่ตอนเล่นและไม่เปลี่ยนรอบการทำงานของกับดัก
