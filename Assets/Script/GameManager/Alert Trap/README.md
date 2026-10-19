# Trap Countdown Alert

ลาก `TrapCountdownAlert.prefab` ไปเป็นลูกของ GameObject ที่ติด `TurnArrowTrap`
หรือ `TurnSpikeTrap` แล้วกด Play ตัวเลขจะอยู่ใกล้กับดักและหันเข้าหากล้อง
เลือกกับดักได้ที่ Target Object (ลาก Prefab instance จาก Hierarchy รองรับสคริปต์อยู่ในลูก)
หรือ Target Trap (ลาก component โดยตรง) ไม่ต้องให้ป้ายเป็นลูกกับดักเสมอไป
ต้องเลือกตัวที่วางอยู่ใน Scene เพราะ Prefab asset ใน Project ไม่มีสถานะเทิร์นขณะเล่น

- ใช้ TurnGameManager และ Interval Turns ของกับดักจริง ไม่ต้องตั้งเวลาซ้ำ
- เลข 1 หมายถึงกับดักจะทำงานใน Trap Phase ตอนจบรอบปัจจุบัน
- แสดงเฉพาะเลข เช่น 3 → 2 → 1 ไม่มีชื่อ พื้นหลัง หน่วย หรือคำว่า ACTIVE
- ระหว่างกับดักทำงาน ตัวเลขจะซ่อน แล้วกลับมานับรอบใหม่เมื่อทำงานจบ
- สีตัวเลขปกติ: เหลือมากกว่า 2 รอบ, สีส้ม: เหลือ 2 รอบ, สีแดง: เหลือ 1 รอบ
- หนามที่ยกค้างจะซ่อนตัวเลขจนกว่าจะถูกสั่งลดลง
- เมื่อกับดักปิดใช้งานหรือเกมจบ ป้ายจะซ่อนเอง
- ป้ายไม่รับ Raycast และไม่มี GraphicRaycaster จึงไม่ขวางการคลิกช่องเดิน

แก้หน้าตาได้ใน Prefab Mode หรือ Hierarchy: Countdown Canvas > Countdown
เลือกจุดยึดที่ Position Anchor เช่น Fire Point และปรับตำแหน่งเพิ่มที่ World Offset
ถ้าต้องการวางเลขเองโดยไม่ตามกับดัก ให้ปิด Follow Target แล้วขยับ Countdown Canvas
ปรับขนาดที่ Scale ของ Countdown Canvas และสีที่ Appearance
ใน Assets/Scenes/Mon/ModelTest.unity วางป้ายไว้กับกับดักธนูฝั่งปลายทางแล้ว

ระบบนี้รองรับกับดักแบบเทิร์นที่สืบทอด TurnTrap เท่านั้น ไม่ใช่ ArrowTrap รุ่นเก่าที่นับวินาที
สคริปต์อ่านสถานะเพื่อแสดงผล ไม่ได้สร้าง UI ใหม่ตอนเล่นและไม่เปลี่ยนรอบการทำงานของกับดัก
