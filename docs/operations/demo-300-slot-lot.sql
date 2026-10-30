-- Smart Parking – Demo lot 300 slots (Tầng A/B/C x 100). IDEMPOTENT.
-- KHÔNG chạy trên Neon dùng chung nếu chưa có phê duyệt của Owner. Chạy trên DB local/staging trước.
-- Cách dùng: đổi v_lot_name bên dưới, chạy trong psql. Chạy lại nhiều lần an toàn (chỉ thêm phần còn thiếu,
-- không đổi trạng thái/is_active của ô hiện có).
DO $$
DECLARE
    v_lot_name  text := 'Bãi Demo 300 Ô';   -- <== đổi nếu cần
    v_address   text := 'Khu demo Smart Parking';
    v_lot_id    int;
    v_floor_id  int;
    r           record;
BEGIN
    SELECT id INTO v_lot_id FROM parking_lots WHERE name = v_lot_name;
    IF v_lot_id IS NULL THEN
        INSERT INTO parking_lots (name, address, total_capacity, is_active, created_at, updated_at)
        VALUES (v_lot_name, v_address, 300, true, now(), now())
        RETURNING id INTO v_lot_id;
    END IF;

    FOR r IN SELECT * FROM (VALUES (1,'A'),(2,'B'),(3,'C')) AS t(num, letter) LOOP
        SELECT id INTO v_floor_id FROM parking_floors WHERE lot_id = v_lot_id AND floor_number = r.num;
        IF v_floor_id IS NULL THEN
            INSERT INTO parking_floors (lot_id, floor_number, name, total_slots, is_active, created_at, updated_at)
            VALUES (v_lot_id, r.num, 'Tầng ' || r.letter, 0, true, now(), now())
            RETURNING id INTO v_floor_id;
        END IF;

        INSERT INTO parking_slots (floor_id, slot_code, physical_status, is_active, created_at, updated_at)
        SELECT v_floor_id, r.letter || lpad(g::text, 3, '0'), 'EMPTY', true, now(), now()
        FROM generate_series(1, 100) AS g
        ON CONFLICT (floor_id, slot_code) DO NOTHING;

        UPDATE parking_floors
           SET total_slots = (SELECT count(*) FROM parking_slots WHERE floor_id = v_floor_id), updated_at = now()
         WHERE id = v_floor_id;
    END LOOP;

    UPDATE parking_lots SET total_capacity = 300, updated_at = now() WHERE id = v_lot_id;
    RAISE NOTICE 'Lot id = %', v_lot_id;
END $$;

-- Kiểm tra: mong đợi physical_slots = 300, usable = 300, configured = 300
SELECT l.id, l.name, l.total_capacity AS configured,
       count(s.id) AS physical_slots,
       count(s.id) FILTER (WHERE s.is_active AND f.is_active AND s.physical_status NOT IN ('MAINTENANCE','UNAVAILABLE')) AS usable
FROM parking_lots l
LEFT JOIN parking_floors f ON f.lot_id = l.id
LEFT JOIN parking_slots s ON s.floor_id = f.id
WHERE l.name = 'Bãi Demo 300 Ô'
GROUP BY l.id, l.name, l.total_capacity;
