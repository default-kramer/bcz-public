#lang racket/gui

(module+ main
  (save-images))

{module barrier-stuff racket/gui
  (provide off-toggle on-toggle W H)

  (require pict)

  (define (tile)
    (define (g)
      (let* ([x (+ 85 (random 31))]
             [color (make-color x x x)])
        (rb-superimpose (rectangle 17 17)
                        (filled-rectangle 16 16 #:color color #:draw-border? #f))))
    (vl-append (hc-append (g) (g) (g) (g))
               (hc-append (g) (g) (g) (g))))

  (define tiles
    (let ()
      (random-seed 42)
      (apply vl-append (for/list ([i (in-range 20)])
                         (apply hc-append (for/list ([i (in-range 20)])
                                            (tile)))))))

  (define tiles-bitmap (pict->bitmap tiles))

  (define W 140)
  (define H 50)

  (define (slider brush)
    (define top 1) ; fudge...
    (define bottom (- H 2)) ; fudge...
    (define left 1)
    (define right (- W 1))
    (dc (lambda (dc dx dy)
          (let* ([W (- right left)]
                 [H (- bottom top)])
            (define old-brush (send dc get-brush))
            (define old-pen (send dc get-pen))
            (send dc set-pen (new pen% [color "black"] [width 2]))
            (send dc set-brush brush)
            (define path (new dc-path%))
            (send path arc top top H H (/ pi 2) (/ pi -2))
            (send path line-to (- right H) bottom)
            (send path arc (- right H) top H H (/ pi -2) (/ pi 2))
            (send path close)
            (send dc draw-path path dx dy)
            (send dc set-pen old-pen)
            (send dc set-brush old-brush)))
        W H))

  (define (make-toggle thing)
    (cc-superimpose (blank (+ W 10) (+ H 6)) ; padding
                    (lc-superimpose thing
                                    (let* ([thickness 4]
                                           [diameter (- H 2)])
                                      (disk diameter #:color (make-color 240 240 240)
                                            #:border-color (make-color 212 212 212)
                                            #:border-width thickness)))))

  (define off-toggle (make-toggle (slider (new brush% [stipple tiles-bitmap]))))

  (define on-toggle (rotate (make-toggle (slider (new brush% [color (make-color 60 175 36)]))) pi))
  }

(require pict)
(require 'barrier-stuff)

(define thickness 40)
(define thickness/2 20)
(define height 240)
(define height/2 120)
(define height/4 60)
(define margin-y 32)
(define spacing 20)

(define numeral-color (make-parameter "black"))

(define (numeral-pen)
  (send the-pen-list find-or-create-pen (numeral-color) thickness 'solid 'round))

(define (make-path points)
  (let ([path (new dc-path%)])
    (send path move-to
          (car (first points))
          (cdr (first points)))
    (for ([point (cdr points)])
      (send path line-to (car point) (cdr point)))
    path))

(define (one)
  (let ([the-pen (numeral-pen)])
    (dc (lambda (dc dx dy)
          (define old-pen (send dc get-pen))
          (send dc set-pen the-pen)
          (define path (make-path (list (cons thickness/2 margin-y)
                                        (cons thickness/2 (- height margin-y)))))
          (send dc draw-path path dx dy)
          (send dc set-pen old-pen))
        thickness height)))

(define (four)
  (let ([w height/2]
        [mid height/4]
        [the-pen (numeral-pen)])
    (dc (lambda (dc dx dy)
          (define old-pen (send dc get-pen))
          (define old-brush (send dc get-brush))
          (send dc set-pen the-pen)
          (send dc set-brush (new brush% [style 'transparent]))
          (define path (make-path (list (cons mid margin-y)
                                        (cons thickness/2 height/2)
                                        (cons mid (- height margin-y))
                                        (cons (- w thickness/2) height/2))))
          (send path close)
          (send dc draw-path path dx dy)
          (send dc set-brush old-brush)
          (send dc set-pen old-pen))
        w height)))

(define (background pict [color #f])
  (let* ([w (+ (pict-width pict) spacing spacing spacing)]
         [bar (filled-rectangle w margin-y #:color (or color (numeral-color)) #:draw-border? #f)])
    (vc-append bar
               (blank (- height margin-y margin-y))
               bar)))

(define (digits i)
  (define (get-picts i)
    (cond
      [(i . >= . 4)
       (cons (four) (get-picts (- i 4)))]
      [(i . >= . 1)
       (cons (one) (get-picts (- i 1)))]
      [else (list)]))
  (apply hc-append (cons spacing (get-picts i))))

(define (numeral i)
  (let* ([dig (digits i)]
         [bg (background dig)]); "lightgray")])
    (cc-superimpose bg dig)))

(define (label toggle i x-shift color)
  (lc-superimpose toggle
                  (hc-append (blank (+ H x-shift) 0)
                             (cc-superimpose
                              (blank H 0)
                              (parameterize ([numeral-color color])
                                (scale (numeral i) 0.15))))))

(define (labelled-off-toggle i)
  (label off-toggle i 15 "orange"))

(define (labelled-on-toggle i)
  (label on-toggle i -15 (make-color 55 55 55)))

(begin
  ;(numeral 1)
  ;(numeral 2)
  (numeral 3)
  ;(numeral 4)
  ;(numeral 5)
  ;(numeral 11)
  ;(numeral 12)
  (numeral 13)

  (labelled-off-toggle 2)
  (labelled-off-toggle 3)
  (labelled-off-toggle 4)
  (labelled-off-toggle 5)
  (labelled-off-toggle 6)
  (labelled-off-toggle 7)
  (labelled-off-toggle 8)
  (labelled-on-toggle 6)
  ;off-toggle
  )

(define (save-images)
  (define results '())
  (define directory (current-directory))
  (define bgcolor (make-color 0 0 0 0))
  (define (save-bitmap pict filename)
    (let* ([bmp (pict->bitmap pict 'aligned)]
           [filename (format "~a~a" directory filename)]
           [quality 100])
      (set! results (cons pict (cons filename results)))
      (send bmp save-file filename 'bmp quality)))
  (for ([i (in-range 1 17)])
    (save-bitmap (digits i) (format "~a.bmp" i)))
  (for ([i (in-range 2 8.5)])
    (save-bitmap (labelled-off-toggle i) (format "toggle-off-~a.bmp" i))
    (save-bitmap (labelled-on-toggle i) (format "toggle-on-~a.bmp" i)))
  (format "Saved images to ~a" directory))
