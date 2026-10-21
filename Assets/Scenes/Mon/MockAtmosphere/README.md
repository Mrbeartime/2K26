# Mock atmosphere

ใช้เฉพาะ Assets/Scenes/Mon/Mock.unity

ใน Hierarchy เปิดกลุ่ม Mock Atmosphere:
- Mist - Deep Abyss / Lower Bank / Beneath Walkway: หมอกฟ้าอมเขียวใต้พื้น 3 ชั้น
- Mist - Optional Surface Veil: ชั้นหมอกคลุมพื้นเพิ่มเติม ปิดไว้เพื่อให้ทางเดินชัด
- Wisps: หมอกฟ้าอมเขียวจางรอบขอบฉาก 4 แผ่น หันตามมุมกล้องที่จัดไว้
- Teal Rim Fill: แสงฟ้าอมเขียวช่วยแยกขอบกำแพง
- Warm Treasure Accent: แสงอุ่นเน้นปลายทางและหีบ

กลุ่ม Mock Foundation คือฐานบล็อกดำด้านใต้พื้น สูง 1.8 unit
ฐานวางแนบใต้พื้นจริงครบ 20 ช่อง รวมช่องที่ติดกันเป็นชิ้นต่อเนื่อง
แก้ความหนา/ตำแหน่งได้จาก Transform ของ Black Base แต่ละชิ้น
เปลี่ยนสีดำหรือความด้านที่ FoundationBlack.mat ฐานไม่มี Collider และไม่แก้กริดเกม

ปรับ LowMist.mat / EdgeWisps.mat:
- Opacity: ความหนาหมอก
- Mist Color: สีหมอก
- World Noise Scale: ขนาดลายหมอก
- Drift X/Z: ความเร็วไหลในเกม
- Depth Intersection Softness: ความฟุ้งเมื่อหมอกแตะวัตถุ

หมอกเป็น MeshRenderer ที่ไม่มี Collider และไม่รับ Raycast จึงไม่ขวางการคลิกเดิน
ตัว Shader รองรับ URP, กล้อง Orthographic และ Depth Texture เปิดไว้เฉพาะกล้อง Mock
แสดงหมอกได้ตั้งแต่ Edit Mode โดยไม่ต้องกด Play และไม่สร้าง GameObject เพิ่มตอนเล่น
หากเปลี่ยนมุมกล้อง ให้หมุน Wisps ให้หันหากล้อง หรือใช้เฉพาะ Mist แนวนอน

MockLook.asset คือ Volume Profile เฉพาะฉาก ปรับ Bloom, Vignette, Tonemapping,
Color Adjustments ได้จาก Global Volume โดยไม่กระทบ Profile ของ Scene อื่น
เปิด/ปิดกลุ่ม Mock Atmosphere เพื่อเปรียบเทียบชั้นหมอกและแสงเสริม
ส่วนไฟหลัก โทนพื้นหลัง และ RenderSettings Fog ปรับแยกใน Scene ตามปกติ
